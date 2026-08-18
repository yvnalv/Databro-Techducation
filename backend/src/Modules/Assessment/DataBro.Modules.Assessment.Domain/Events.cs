using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Assessment.Domain;

/// <summary>
/// Raised when a learner submits an attempt.
///
/// <para>
/// Deliberately <b>not</b> an integration event yet. Nothing consumes it: whether passing a quiz
/// gates lesson completion is a real decision that has not been made, and publishing an event before
/// anything listens would be the speculative move ADR-0014 and ADR-0017 both declined. Promoting it
/// later is one interface and one registry line.
/// </para>
/// <para>
/// It carries <c>LessonId</c> so that promotion does not need the quiz re-read — a subscriber in
/// Learning would otherwise have to ask Assessment which lesson this was about, which is a
/// cross-module call to learn something the event already knew.
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
