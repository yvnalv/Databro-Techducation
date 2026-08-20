namespace DataBro.Api;

/// <summary>
/// Finds the repository-root <c>.env</c> by walking up from a starting directory (ADR-0019).
///
/// <para>
/// The API runs from its own build output several levels below the repo root, where <c>.env</c> lives.
/// A deployed container ships no <c>.env</c> at all — configuration arrives as real environment
/// variables — so <see cref="Find"/> returns <c>null</c> there and the caller loads nothing.
/// </para>
/// </summary>
public static class DotEnvLoader
{
    public static string? Find(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
