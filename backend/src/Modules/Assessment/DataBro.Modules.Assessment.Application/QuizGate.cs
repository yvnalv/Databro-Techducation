using DataBro.Platform.Abstractions;

namespace DataBro.Modules.Assessment.Application;

/// <summary>
/// Assessment's answer to Learning's <see cref="IQuizGate"/> (AS-9 / D-1). See the interface for why
/// this is a query the caller makes at decision time rather than an event Assessment publishes.
/// </summary>
public sealed class QuizGate(IQuizRepository quizzes, IQuizAttemptRepository attempts) : IQuizGate
{
    public async Task<bool> IsCompletionBlockedAsync(
        Guid userId, Guid lessonId, CancellationToken ct = default)
    {
        // No published quiz, no gate: a lesson without one completes exactly as it did before D-1.
        // The draft of a quiz does not gate anything — only a published one is a promise to the
        // learner that the lesson expects it.
        var quiz = await quizzes.GetPublishedForLessonAsync(lessonId, ct);
        if (quiz is null) return false;

        return !await attempts.HasPassedAsync(userId, quiz.Id, ct);
    }
}
