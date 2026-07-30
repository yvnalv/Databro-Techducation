using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// Maps a POCO property to a PostgreSQL <c>jsonb</c> column via System.Text.Json.
/// Used for content blocks and SEO metadata (ADR-0004).
/// </summary>
internal static class JsonbConversion
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<T> HasJsonbConversion<T>(this PropertyBuilder<T> builder)
        where T : class
    {
        var converter = new ValueConverter<T, string>(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<T>(v, Options)!);

        var comparer = new ValueComparer<T>(
            (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
            v => JsonSerializer.Serialize(v, Options).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!);

        builder.HasConversion(converter, comparer).HasColumnType("jsonb");
        return builder;
    }
}
