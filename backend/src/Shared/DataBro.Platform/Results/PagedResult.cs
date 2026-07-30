namespace DataBro.Platform.Results;

/// <summary>
/// A page of results plus the total row count.
///
/// <para>
/// Offset-based rather than cursor-based, deliberately. docs/API_SPEC.md §3 prefers cursors for
/// public listings, but category and tag pages exist to be crawled, and a cursor has no stable,
/// linkable URL a crawler can enumerate. Indexable listings therefore use page numbers; cursors
/// remain the right choice for feeds that are not indexed. See docs/SEO.md.
/// </para>
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}

/// <summary>
/// A validated page request. Clamped on construction so a hostile or careless
/// <c>?pageSize=100000</c> cannot turn a public endpoint into a denial-of-service lever.
/// </summary>
public readonly record struct PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }

    public PageRequest(int? page = null, int? pageSize = null)
    {
        Page = page is null or < 1 ? 1 : page.Value;
        PageSize = pageSize switch
        {
            null or < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value,
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
