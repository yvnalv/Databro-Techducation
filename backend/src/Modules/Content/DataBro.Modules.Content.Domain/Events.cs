using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>Raised when an article is published. Search reindex / cache invalidation react to this.</summary>
public sealed record ArticlePublishedDomainEvent(Guid ArticleId, string Slug, int Version) : IDomainEvent;

/// <summary>Raised when a published article is taken down.</summary>
public sealed record ArticleUnpublishedDomainEvent(Guid ArticleId, string Slug) : IDomainEvent;

// Lesson bodies raise their own events rather than reusing the article ones (ADR-0012). A subscriber
// reacting to `ArticlePublished` — cache invalidation for a public article URL, say, or a future
// search reindex — would be acting on something that is not an article and has no such URL.

/// <summary>Raised when a lesson's body is published.</summary>
public sealed record LessonContentPublishedDomainEvent(Guid LessonContentId, string Slug, int Version) : IDomainEvent;

/// <summary>Raised when a published lesson body is taken down.</summary>
public sealed record LessonContentUnpublishedDomainEvent(Guid LessonContentId, string Slug) : IDomainEvent;
