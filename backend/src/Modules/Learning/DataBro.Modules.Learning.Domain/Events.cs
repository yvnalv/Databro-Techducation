using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>Raised when a course goes live. Cache invalidation and, later, search reindex react to this.</summary>
public sealed record CoursePublishedDomainEvent(Guid CourseId, string Slug) : IDomainEvent;

/// <summary>Raised when a published course is taken down.</summary>
public sealed record CourseUnpublishedDomainEvent(Guid CourseId, string Slug) : IDomainEvent;
