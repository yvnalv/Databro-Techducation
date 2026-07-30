using DataBro.Platform.Authorization;

namespace DataBro.Modules.Identity.Domain;

/// <summary>
/// Phase 1 roles and their permission grants (docs/SECURITY.md §2). Permission names come from the
/// shared <see cref="Permissions"/> vocabulary; this map is Identity's authorization policy.
/// Publishing is distinct from authoring.
/// </summary>
public static class Roles
{
    public const string Reader = "Reader";
    public const string Author = "Author";
    public const string Editor = "Editor";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Reader, Author, Editor, Admin];

    /// <summary>The default role assigned on self-registration.</summary>
    public const string Default = Reader;

    public static readonly IReadOnlyDictionary<string, string[]> Grants = new Dictionary<string, string[]>
    {
        [Reader] = [Permissions.ContentView],
        [Author] =
        [
            Permissions.ContentView, Permissions.ContentCreate, Permissions.ContentEdit,
            Permissions.MediaUpload,
        ],
        [Editor] =
        [
            Permissions.ContentView, Permissions.ContentCreate, Permissions.ContentEdit,
            Permissions.ContentPublish, Permissions.ContentDelete, Permissions.TaxonomyManage,
            Permissions.MediaUpload,
        ],
        [Admin] = [.. Permissions.All],
    };
}
