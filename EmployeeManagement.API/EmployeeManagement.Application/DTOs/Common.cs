namespace EmployeeManagement.Application.DTOs.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class QueryParams
{
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber { get => _pageNumber; set => _pageNumber = value < 1 ? 1 : value; }
    public int PageSize { get => _pageSize; set => _pageSize = value is < 1 or > 200 ? 20 : value; }

    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
