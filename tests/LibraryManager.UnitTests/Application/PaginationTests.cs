using LibraryManager.Application.Common;

namespace LibraryManager.UnitTests.Application;

public sealed class PaginationTests
{
    [Fact]
    public void Normalize_applies_defaults_for_invalid_values()
    {
        var (page, pageSize) = Pagination.Normalize(0, 0);

        Assert.Equal(Pagination.DefaultPage, page);
        Assert.Equal(Pagination.DefaultPageSize, pageSize);
    }

    [Fact]
    public void Normalize_caps_page_size_at_100()
    {
        var (page, pageSize) = Pagination.Normalize(2, 1000);

        Assert.Equal(2, page);
        Assert.Equal(Pagination.MaxPageSize, pageSize);
    }
}
