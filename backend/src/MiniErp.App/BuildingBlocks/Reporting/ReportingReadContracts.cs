#pragma warning disable CS1591

namespace MiniErp.App.BuildingBlocks.Reporting;

/// <summary>
/// A bounded, typed page request shared by source-owned reporting read ports.
/// The source owns filtering, ordering and paging; Reporting only coordinates
/// the resulting page.
/// </summary>
public sealed record ReportingPageRequest(
    int Page,
    int PageSize,
    string? SortBy = null,
    string SortDirection = "asc")
{
    public int Offset => checked((Page - 1) * PageSize);

    public static ReportingPageRequest Create(int page, int pageSize, string? sortBy, string? sortDirection)
    {
        if (page < 1 || pageSize is < 1 or > 500)
            throw new ArgumentException("Page bounds are invalid.");

        var direction = string.IsNullOrWhiteSpace(sortDirection) ? "asc" : sortDirection.Trim().ToLowerInvariant();
        if (direction is not ("asc" or "desc"))
            throw new ArgumentException("Sort direction is invalid.");

        return new ReportingPageRequest(page, pageSize, string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim(), direction);
    }
}

/// <summary>Source-owned page output with explicit count and freshness facts.</summary>
public sealed record ReportingSourcePage<T>(
    IReadOnlyList<T> Rows,
    int TotalRows,
    string StableSortKey,
    DateTimeOffset? DataAsOf,
    bool TotalRowsKnown = true)
{
    public static ReportingSourcePage<T> Empty(string stableSortKey) => new([], 0, stableSortKey, null);
}

#pragma warning restore CS1591
