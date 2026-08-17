using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>Raised when a course goes live. Cache invalidation and, later, search reindex react to this.</summary>
public sealed record CoursePublishedDomainEvent(Guid CourseId, string Slug) : IDomainEvent;

/// <summary>Raised when a published course is taken down.</summary>
public sealed record CourseUnpublishedDomainEvent(Guid CourseId, string Slug) : IDomainEvent;

/// <summary>Raised when a learner joins a course. Welcome email and analytics will react to this.</summary>
public sealed record EnrolledDomainEvent(Guid EnrollmentId, Guid UserId, Guid CourseId) : IDomainEvent;

/// <summary>
/// Raised once, on the transition to complete — never on a later save of an already-complete
/// enrollment. Certificates and completion notifications hang off this, and both are things a
/// learner must not receive twice.
/// </summary>
public sealed record CourseCompletedDomainEvent(
    Guid EnrollmentId,
    Guid UserId,
    Guid CourseId,
    DateTimeOffset CompletedAt) : IDomainEvent;
