using LibraryManager.Application.Common;

namespace LibraryManager.Api.Contracts.Common;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public static PagedResponse<T> From<TSource>(
        PagedResult<TSource> source,
        Func<TSource, T> map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);

        return new(
            source.Items.Select(map).ToList(),
            source.Page,
            source.PageSize,
            source.TotalCount);
    }
}
