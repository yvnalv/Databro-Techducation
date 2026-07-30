namespace DataBro.Platform.Authorization;

/// <summary>
/// Canonical permission names (docs/SECURITY.md §2) — a shared authorization vocabulary so any module
/// can require a permission and Identity can grant it, without modules depending on each other.
/// The role → permission mapping (policy) lives in the Identity module.
/// </summary>
public static class Permissions
{
    public const string ContentView = "Content.View";
    public const string ContentCreate = "Content.Create";
    public const string ContentEdit = "Content.Edit";
    public const string ContentPublish = "Content.Publish";
    public const string ContentDelete = "Content.Delete";
    public const string TaxonomyManage = "Taxonomy.Manage";
    public const string MediaUpload = "Media.Upload";
    public const string UserManage = "User.Manage";

    public static readonly IReadOnlyList<string> All =
    [
        ContentView, ContentCreate, ContentEdit, ContentPublish, ContentDelete,
        TaxonomyManage, MediaUpload, UserManage,
    ];
}
