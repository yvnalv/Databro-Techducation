using System.Reflection;
using DataBro.Modules.Learning.Domain;
using DataBro.Platform.SharedKernel;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// The curriculum rules from ADR-0013. The ordering assertions matter most: contiguous integers are
/// only trustworthy if nothing can leave a gap, and every read downstream relies on that.
/// </summary>
public class CourseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static Course NewCourse() =>
        Course.CreateDraft(Guid.NewGuid(), Slug.Create("rag-end-to-end"), "RAG, End to End", "A summary");

    /// <summary>A course with one module holding <paramref name="lessons"/> lessons.</summary>
    private static (Course Course, CourseModule Module) WithLessons(int lessons)
    {
        var course = NewCourse();
        var module = course.AddModule(Guid.NewGuid(), "Retrieval");

        for (var i = 0; i < lessons; i++)
            module.AddLesson(Guid.NewGuid(), Guid.NewGuid());

        return (course, module);
    }

    // ---- Ordering (ADR-0013 §2) ----

    [Fact]
    public void Modules_are_numbered_contiguously_from_zero()
    {
        var course = NewCourse();
        course.AddModule(Guid.NewGuid(), "One");
        course.AddModule(Guid.NewGuid(), "Two");
        course.AddModule(Guid.NewGuid(), "Three");

        Assert.Equal([0, 1, 2], course.Modules.Select(m => m.Order));
    }

    [Fact]
    public void Removing_a_module_closes_the_gap()
    {
        // The whole reason normalisation runs after every structural change. A gap here would make
        // "the third module" ambiguous forever after.
        var course = NewCourse();
        var first = course.AddModule(Guid.NewGuid(), "One");
        course.AddModule(Guid.NewGuid(), "Two");
        course.AddModule(Guid.NewGuid(), "Three");

        course.RemoveModule(first.Id);

        Assert.Equal([0, 1], course.Modules.Select(m => m.Order));
        Assert.Equal(["Two", "Three"], course.Modules.Select(m => m.Title));
    }

    [Fact]
    public void Adding_a_module_survives_an_unordered_reload_of_the_existing_ones()
    {
        // The regression behind this test: every authoring call reloads the aggregate, and the EF
        // include does not order the modules collection, so the backing list can materialise in any
        // order. NormaliseModules used to renumber by that raw position, which let a third AddModule
        // rewrite the first two modules' Order to whatever sequence the database happened to return —
        // silently reshuffling a saved curriculum. Here we reproduce that by reversing the backing
        // list to stand in for an unordered load, then adding a module: order must still come from
        // each module's Order, not from where it sits in the list.
        var course = NewCourse();
        var a = course.AddModule(Guid.NewGuid(), "A");
        var b = course.AddModule(Guid.NewGuid(), "B");

        ReversePrivateList(course, "_modules");

        var c = course.AddModule(Guid.NewGuid(), "C");

        Assert.Equal(["A", "B", "C"], course.Modules.Select(m => m.Title));
        Assert.Equal([0, 1, 2], course.Modules.Select(m => m.Order));
        _ = (a, b, c);
    }

    /// <summary>
    /// Reverses an aggregate's private child list in place, standing in for EF materialising a
    /// collection include in an order other than by <c>Order</c> — the condition the ordering fix
    /// hardens against and that a normal in-memory test never reproduces.
    /// </summary>
    private static void ReversePrivateList(object owner, string field)
    {
        var list = (System.Collections.IList)owner
            .GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(owner)!;

        var items = list.Cast<object>().Reverse().ToList();
        list.Clear();
        foreach (var item in items) list.Add(item);
    }

    [Fact]
    public void Reordering_modules_renumbers_them()
    {
        var course = NewCourse();
        var a = course.AddModule(Guid.NewGuid(), "A");
        var b = course.AddModule(Guid.NewGuid(), "B");
        var c = course.AddModule(Guid.NewGuid(), "C");

        var result = course.ReorderModules([c.Id, a.Id, b.Id]);

        Assert.True(result.IsSuccess);
        Assert.Equal(["C", "A", "B"], course.Modules.Select(m => m.Title));
        Assert.Equal([0, 1, 2], course.Modules.Select(m => m.Order));
    }

    [Fact]
    public void A_partial_reorder_keeps_the_unnamed_ones_after_in_their_old_order()
    {
        // A builder UI that sends only the moved subset must not silently drop the rest.
        var course = NewCourse();
        var a = course.AddModule(Guid.NewGuid(), "A");
        var b = course.AddModule(Guid.NewGuid(), "B");
        var c = course.AddModule(Guid.NewGuid(), "C");

        course.ReorderModules([c.Id]);

        Assert.Equal(["C", "A", "B"], course.Modules.Select(m => m.Title));
        Assert.Equal(3, course.Modules.Count);
        _ = (a, b);
    }

    [Fact]
    public void A_reorder_naming_an_unknown_module_is_refused()
    {
        var course = NewCourse();
        var a = course.AddModule(Guid.NewGuid(), "A");

        var result = course.ReorderModules([a.Id, Guid.NewGuid()]);

        Assert.True(result.IsFailure);
        Assert.Equal("validation_failed", result.Error.Code);
    }

    [Fact]
    public void A_reorder_listing_the_same_module_twice_is_refused()
    {
        var course = NewCourse();
        var a = course.AddModule(Guid.NewGuid(), "A");
        course.AddModule(Guid.NewGuid(), "B");

        Assert.True(course.ReorderModules([a.Id, a.Id]).IsFailure);
    }

    [Fact]
    public void Lessons_are_ordered_and_renumbered_within_their_module()
    {
        var (_, module) = WithLessons(3);
        var lessons = module.Lessons;

        Assert.Equal([0, 1, 2], lessons.Select(l => l.Order));

        module.ReorderLessons([lessons[2].Id, lessons[0].Id, lessons[1].Id]);
        Assert.Equal([0, 1, 2], module.Lessons.Select(l => l.Order));
        Assert.Equal(lessons[2].Id, module.Lessons[0].Id);

        module.RemoveLesson(module.Lessons[0].Id);
        Assert.Equal([0, 1], module.Lessons.Select(l => l.Order));
    }

    // ---- Lessons ----

    [Fact]
    public void The_same_body_cannot_appear_twice_in_one_module()
    {
        // Duplicated lessons make "completed" ambiguous the moment progress exists.
        var course = NewCourse();
        var module = course.AddModule(Guid.NewGuid(), "Retrieval");
        var body = Guid.NewGuid();

        Assert.True(module.AddLesson(Guid.NewGuid(), body).IsSuccess);

        var duplicate = module.AddLesson(Guid.NewGuid(), body);
        Assert.True(duplicate.IsFailure);
        Assert.Equal("conflict", duplicate.Error.Code);
    }

    [Fact]
    public void Removing_a_lesson_clears_it_from_other_lessons_prerequisites()
    {
        // A dangling prerequisite would require something no longer in the curriculum, which nothing
        // downstream could resolve.
        var (_, module) = WithLessons(2);
        var first = module.Lessons[0];
        var second = module.Lessons[1];

        second.RequirePrerequisites([first.Id]);
        Assert.Contains(first.Id, second.PrerequisiteLessonIds);

        module.RemoveLesson(first.Id);

        Assert.Empty(module.Lessons.Single().PrerequisiteLessonIds);
    }

    [Fact]
    public void A_lesson_cannot_be_its_own_prerequisite()
    {
        var (_, module) = WithLessons(1);
        var lesson = module.Lessons.Single();

        lesson.RequirePrerequisites([lesson.Id]);

        Assert.Empty(lesson.PrerequisiteLessonIds);
    }

    [Fact]
    public void Objectives_are_trimmed_and_blanks_dropped()
    {
        var (_, module) = WithLessons(1);
        var lesson = module.Lessons.Single();

        lesson.Describe(20, Difficulty.Intermediate, ["  Chunk a document  ", "   ", "Evaluate recall"]);

        Assert.Equal(["Chunk a document", "Evaluate recall"], lesson.Objectives);
        Assert.Equal(20, lesson.EstimatedMinutes);
        Assert.Equal(Difficulty.Intermediate, lesson.Difficulty);
    }

    [Fact]
    public void Estimated_time_is_summed_from_the_lessons()
    {
        // Derived rather than stored, so it cannot drift from the curriculum it describes.
        var (course, module) = WithLessons(3);

        foreach (var lesson in module.Lessons)
            lesson.Describe(15, Difficulty.Beginner);

        Assert.Equal(45, course.EstimatedMinutes);
        Assert.Equal(3, course.LessonCount);
    }

    // ---- Publishing (ADR-0013 §1) ----

    [Fact]
    public void A_course_publishes_without_requiring_its_lessons_to_be_published()
    {
        // The decision that keeps a large curriculum publishable. The course knows nothing about
        // whether its lesson bodies are live — that is Content's business, resolved at read time.
        var (course, _) = WithLessons(2);

        var result = course.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(CourseStatus.Published, course.Status);
        Assert.Equal(Now, course.PublishedAt);
        Assert.Contains(course.DomainEvents, e => e is CoursePublishedDomainEvent);
    }

    [Fact]
    public void An_empty_course_cannot_be_published()
    {
        var course = NewCourse();
        course.AddModule(Guid.NewGuid(), "Empty section");

        var result = course.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("business_rule_violation", result.Error.Code);
    }

    [Fact]
    public void Only_a_published_course_can_be_unpublished()
    {
        var (course, _) = WithLessons(1);

        Assert.Equal("conflict", course.Unpublish().Error.Code);

        course.Publish(Now);
        Assert.True(course.Unpublish().IsSuccess);
        Assert.Equal(CourseStatus.Unpublished, course.Status);
        Assert.Contains(course.DomainEvents, e => e is CourseUnpublishedDomainEvent);
    }

    [Fact]
    public void Changing_the_slug_reports_the_previous_one_so_a_redirect_can_be_written()
    {
        var course = NewCourse();

        var previous = course.ChangeSlug(Slug.Create("rag-in-practice"));

        Assert.Equal("rag-end-to-end", previous!.Value);
        Assert.Null(course.ChangeSlug(Slug.Create("rag-in-practice")));
    }
}
