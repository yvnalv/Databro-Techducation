using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>Raised when an article is published. Search reindex / cache invalidation react to this.</summary>
public sealed record ArticlePublishedDomainEvent(Guid ArticleId, string Slug, int Version) : IDomainEvent;

/// <summary>Raised when a published article is taken down.</summary>
public sealed record ArticleUnpublishedDomainEvent(Guid ArticleId, string Slug) : IDomainEvent;
