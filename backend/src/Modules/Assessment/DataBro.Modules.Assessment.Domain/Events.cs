using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Assessment.Domain;

/// <summary>
/// Raised when a learner submits an attempt.
///
/// <para>
/// Still <b>not</b> an integration event, even though the question it was kept for — does passing a
/// quiz gate lesson completion — has now been answered yes (D-1). That gate is a synchronous query
/// (<see cref="DataBro.Platform.Abstractions.IQuizGate"/>) rather than a subscription to this event,
/// because a decision-time check cannot be eventually consistent without refusing a learner who has
/// just passed. So this event still has no consumer, and publishing it through an outbox nothing
/// reads would be the speculative move ADR-0017 declined. It stays a domain event, earning its place
/// the day a genuinely eventual effect — a notification, an analytics roll-up — wants it.
/// </para>
/// <para>
/// It carries <c>LessonId</c> so that such a subscriber does not need the quiz re-read — it would
/// otherwise have to ask Assessment which lesson this was about, a cross-module call to learn
/// something the event already knew.
/// </para>
/// </summary>
public sealed record QuizAttemptSubmittedDomainEvent(
    Guid AttemptId,
    Guid UserId,
    Guid QuizId,
    Guid LessonId,
    int Score,
    int TotalPoints,
    bool Passed,
    DateTimeOffset SubmittedAt) : IDomainEvent;
