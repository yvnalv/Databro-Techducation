using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Content.Domain;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Modules.Identity.Domain;
using DataBro.Modules.Learning.Domain;
using DataBro.Modules.Learning.Infrastructure.Persistence;
using DataBro.Platform.Messaging;
using DataBro.Platform.Persistence.Outbox;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// The transactional outbox.
///
/// What is worth testing is not "a row appears" but the guarantee: the row and the state change that
/// caused it are committed together, the message is dispatched once the worker runs and not again
/// afterwards, and a handler that fails is retried rather than lost.
/// </summary>
public class OutboxTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<Guid> SeedBodyAsync(string title)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        var body = LessonContent.CreateDraft(
            Guid.NewGuid(), Slug.Create($"body-{Guid.NewGuid():N}"), title, "Summary",
            new ContentDocument
            {
                Version = 1,
                Blocks =
                [
                    new ContentBlock
                    {
                        Id = "b0",
                        Type = "paragraph",
                        Data = new System.Text.Json.Nodes.JsonObject { ["text"] = "Body." },
                    },
                ],
            });

        body.Publish(DateTimeOffset.UtcNow);
        db.LessonContents.Add(body);
        await db.SaveChangesAsync();

        return body.Id;
    }

    /// <summary>A published one-lesson course. Returns its slug and that lesson's id.</summary>
    private async Task<(string Slug, Guid LessonId)> SeedCourseAsync()
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var slug = $"course-{Guid.NewGuid():N}";

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses",
            new { title = "Outbox Course", summary = "One lesson", slug }));
        var courseId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withModule = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Module" }));
        var moduleId = withModule.GetProperty("data").GetProperty("modules")[0].GetProperty("id").GetGuid();

        (await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
            new { contentUnitId = await SeedBodyAsync("Only Lesson") })).EnsureSuccessStatusCode();

        (await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null))
            .EnsureSuccessStatusCode();

        var page = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/courses/{slug}"));
        var lessonId = page.GetProperty("data").GetProperty("modules")[0]
            .GetProperty("lessons")[0].GetProperty("id").GetGuid();

        return (slug, lessonId);
    }

    private static async Task<List<OutboxMessage>> MessagesAsync(IServiceScope scope) =>
        await scope.ServiceProvider.GetRequiredService<LearningDbContext>()
            .Set<OutboxMessage>()
            .OrderBy(m => m.OccurredAt)
            .ToListAsync();

    private async Task<int> DrainAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<OutboxProcessor<LearningDbContext>>()
            .ProcessBatchAsync();
    }

    [Fact]
    public async Task Completing_a_course_queues_a_message_alongside_the_completion()
    {
        var (slug, lessonId) = await SeedCourseAsync();
        var learner = await factory.CreateAuthenticatedClientAsync(Roles.Reader);
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null)).EnsureSuccessStatusCode();

        // Clear anything an earlier test left pending, so the assertion below is about this
        // completion and not about test ordering. DrainAsync makes its own scope.
        await DrainAsync();

        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null))
            .EnsureSuccessStatusCode();

        using var after = factory.Services.CreateScope();
        var pending = (await MessagesAsync(after)).Where(m => m.ProcessedAt is null).ToList();

        var queued = Assert.Single(pending);
        Assert.Equal("learning.course-completed", queued.Type);
        Assert.Contains("completedAt", queued.Payload);
        Assert.Equal(0, queued.Attempts);
    }

    [Fact]
    public async Task Only_events_the_registry_knows_are_queued()
    {
        // Enrolling raises EnrolledDomainEvent, which is deliberately *not* an integration event.
        // Publishing everything an aggregate raises would make every internal rename someone else's
        // breaking change.
        var (slug, _) = await SeedCourseAsync();
        await DrainAsync();

        using (var before = factory.Services.CreateScope())
        {
            Assert.DoesNotContain(await MessagesAsync(before), m => m.ProcessedAt is null);
        }

        var learner = await factory.CreateAuthenticatedClientAsync(Roles.Reader);
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null)).EnsureSuccessStatusCode();

        using var after = factory.Services.CreateScope();
        Assert.DoesNotContain(await MessagesAsync(after), m => m.ProcessedAt is null);
    }

    [Fact]
    public async Task The_processor_dispatches_and_marks_the_message_processed()
    {
        var (slug, lessonId) = await SeedCourseAsync();
        var learner = await factory.CreateAuthenticatedClientAsync(Roles.Reader);
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null)).EnsureSuccessStatusCode();
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null))
            .EnsureSuccessStatusCode();

        var handled = await DrainAsync();

        Assert.True(handled > 0);

        using var after = factory.Services.CreateScope();
        var messages = await MessagesAsync(after);

        Assert.All(messages, m => Assert.NotNull(m.ProcessedAt));
        Assert.All(messages, m => Assert.False(m.IsDeadLettered));
        Assert.All(messages, m => Assert.Null(m.Error));
    }

    [Fact]
    public async Task A_processed_message_is_not_dispatched_again()
    {
        // At-least-once is the guarantee, but a message already marked processed must not be picked
        // up by the next sweep — otherwise every handler runs on every pass, forever.
        var (slug, lessonId) = await SeedCourseAsync();
        var learner = await factory.CreateAuthenticatedClientAsync(Roles.Reader);
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null)).EnsureSuccessStatusCode();
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null))
            .EnsureSuccessStatusCode();

        await DrainAsync();
        var second = await DrainAsync();

        Assert.Equal(0, second);
    }

    [Fact]
    public void A_contract_name_cannot_be_reused_for_a_second_type()
    {
        // Two types under one name makes deserialisation a coin toss. Worth failing at startup
        // rather than discovering it in a dead-letter row.
        var registry = new OutboxRegistry();
        registry.Register<CourseCompletedDomainEvent>("learning.course-completed");

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(typeof(OtherEvent), "learning.course-completed"));
    }

    [Fact]
    public void Backoff_grows_between_attempts_and_parks_after_the_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var message = OutboxMessage.Create(Guid.NewGuid(), "t", "{}", now);

        message.MarkFailed("boom", now, maxAttempts: 3);
        var first = message.NextAttemptAt;

        message.MarkFailed("boom", now, maxAttempts: 3);
        var second = message.NextAttemptAt;

        Assert.True(second > first, "backoff should grow between attempts");
        Assert.False(message.IsDeadLettered);

        message.MarkFailed("boom", now, maxAttempts: 3);

        Assert.True(message.IsDeadLettered);
        // Parked, not deleted, and with nothing scheduled — a dead-lettered effect is exactly what
        // someone needs to be able to read afterwards.
        Assert.Null(message.NextAttemptAt);
        Assert.Equal("boom", message.Error);
    }

    private sealed record OtherEvent : IIntegrationEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
