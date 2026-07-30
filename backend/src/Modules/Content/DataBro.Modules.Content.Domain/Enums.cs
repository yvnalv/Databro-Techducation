namespace DataBro.Modules.Content.Domain;

/// <summary>Lifecycle status of a content unit (docs/CONTENT_MODEL.md §4).</summary>
public enum ArticleStatus
{
    Draft = 0,
    Scheduled = 1,
    Published = 2,
    Unpublished = 3,
    Archived = 4,
}

/// <summary>Access visibility. Premium gating activates in Phase 3; reserved from day one.</summary>
public enum Visibility
{
    Public = 0,
    Premium = 1,
}
