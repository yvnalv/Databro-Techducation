using System.Text.RegularExpressions;

namespace DataBro.Platform.SharedKernel;

/// <summary>
/// A URL-safe slug (lowercase, hyphen-separated). Immutable once its content is published
/// (rule CT-2). Value object with validation.
///
/// <para>
/// In the shared kernel rather than in Content, because more than one module now needs it: a course
/// has a public URL exactly as an article does, and Learning cannot reference Content's types
/// (CLAUDE.md rule 10). Nothing here is content-specific — it is a URL primitive.
/// </para>
/// </summary>
public sealed partial class Slug : IEquatable<Slug>
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Slug cannot be empty.", nameof(input));

        var normalized = input.Trim().ToLowerInvariant();

        if (!SlugPattern().IsMatch(normalized))
            throw new ArgumentException(
                $"'{input}' is not a valid slug (use lowercase letters, digits and hyphens).",
                nameof(input));

        return new Slug(normalized);
    }

    /// <summary>Best-effort conversion of arbitrary text (e.g. a title) into a valid slug.</summary>
    public static Slug FromText(string text)
    {
        var lowered = (text ?? string.Empty).Trim().ToLowerInvariant();
        var hyphenated = NonSlugChars().Replace(lowered, "-");
        var collapsed = MultiHyphen().Replace(hyphenated, "-").Trim('-');
        return Create(collapsed);
    }

    public bool Equals(Slug? other) => other is not null && other.Value == Value;
    public override bool Equals(object? obj) => obj is Slug s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiHyphen();
}
