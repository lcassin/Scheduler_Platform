namespace SchedulerPlatform.API.Models;

/// <summary>
/// Generic paged response wrapper for API endpoints that return paginated data.
/// Provides consistent pagination metadata across all list endpoints.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// The items for the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

    /// <summary>
    /// Total count of all items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-indexed).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Creates a new PagedResponse with the specified items and pagination info.
    /// </summary>
    public PagedResponse() { }

    /// <summary>
    /// Creates a new PagedResponse with the specified items and pagination info.
    /// </summary>
    /// <param name="items">The items for the current page.</param>
    /// <param name="totalCount">Total count of all items.</param>
    /// <param name="pageNumber">Current page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    public PagedResponse(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
