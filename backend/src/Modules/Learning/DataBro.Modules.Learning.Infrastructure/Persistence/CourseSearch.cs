using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

/// <summary>
/// Learning's segment of the search results (ADR-0014). Searches courses and nothing else — Learning
/// owns them, and no other module gets to.
/// </summary>
internal sealed class CourseSearch(LearningDbContext db) : IModuleSearch
{
    public string Kind => "courses";

    /// <summary>Before articles: a course is the larger commitment and the rarer, better answer.</summary>
    public int Order => 0;

    /// <summary>
    /// Matches the article fallback's threshold, so a typo behaves the same whichever segment it
    /// lands in. Inconsistent typo tolerance between segments reads as a bug, not a nuance.
    /// </summary>
    private const double FuzzyThreshold = 0.3;

    public async Task<SearchSegment> SearchAsync(
        string query, string locale, int limit, CancellationToken ct = default)
    {
        // Courses carry no locale of their own — the curriculum is English until it is translated —
        // so the parameter is accepted for contract symmetry and deliberately unused.
        _ = locale;

        var published = db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published);

        var exact = await published
            .Where(c => EF.Property<NpgsqlTsVector>(c, CourseConfiguration.SearchVectorProperty)
                .Matches(EF.Functions.WebSearchToTsQuery("english", query)))
            .OrderByDescending(c => EF.Property<NpgsqlTsVector>(c, CourseConfiguration.SearchVectorProperty)
                .Rank(EF.Functions.WebSearchToTsQuery("english", query)))
            .Take(limit)
            .ToListAsync(ct);

        if (exact.Count > 0)
            return Segment(exact, "exact");

        // Same fallback as articles: `word_similarity`, not `similarity`, because whole-string
        // similarity divides by the title's length and a one-word typo against a long title scores
        // too low to match anything.
        var fuzzy = await published
            .Where(c => EF.Functions.TrigramsWordSimilarity(query, c.Title) > FuzzyThreshold)
            .OrderByDescending(c => EF.Functions.TrigramsWordSimilarity(query, c.Title))
            .Take(limit)
            .ToListAsync(ct);

        // Reported as `exact` when the fallback also found nothing: there is no approximation to
        // apologise for, just no results.
        return Segment(fuzzy, fuzzy.Count > 0 ? "fuzzy" : "exact");
    }

    private SearchSegment Segment(IReadOnlyList<Course> courses, string matchMode) =>
        new(
            Kind,
            courses
                .Select(c => new SearchHit(c.Id, c.Slug.Value, $"/courses/{c.Slug.Value}", c.Title, c.Summary))
                .ToList(),
            courses.Count,
            matchMode);
}
