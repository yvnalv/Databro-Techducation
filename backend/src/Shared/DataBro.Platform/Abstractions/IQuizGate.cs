namespace DataBro.Platform.Abstractions;

/// <summary>
/// Whether a lesson's quiz stands between a learner and completing it (AS-9, decided in D-1).
///
/// <para>
/// Lives in <c>Platform</c>, implemented by Assessment, consumed by Learning — the same shape as
/// <see cref="IUserDirectory"/> and <see cref="ILessonContentReader"/>, so Learning asks the question
/// without ever learning that Assessment exists (ADR-0008).
/// </para>
/// <para>
/// <b>A synchronous query, deliberately, rather than a subscription to <c>QuizAttemptSubmitted</c>.</b>
/// The answer has to be right at the instant a learner marks a lesson complete. An event fed through
/// the outbox would arrive eventually, and "eventually" is exactly the window in which a learner who
/// passed the quiz a second ago and clicked <i>complete</i> would be told they had not — the honest
/// consequence of gating a decision-time check on an eventually-consistent copy. So Learning asks, and
/// Assessment answers from its own current state.
/// </para>
/// </summary>
public interface IQuizGate
{
    /// <summary>
    /// True when the lesson has a published quiz the learner has not yet passed. False in the two
    /// cases that let completion through: there is no published quiz, or a passing attempt exists.
    /// </summary>
    Task<bool> IsCompletionBlockedAsync(Guid userId, Guid lessonId, CancellationToken ct = default);
}
