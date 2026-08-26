using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class BookTests
{
    [Fact]
    public void Create_sets_available_copies_equal_to_total_and_is_active()
    {
        var now = DateTime.UtcNow;

        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 3, now);

        Assert.Equal("Dune", book.Title);
        Assert.Equal("9780441172719", book.Isbn);
        Assert.Equal("Frank Herbert", book.Author);
        Assert.Equal(3, book.TotalCopies);
        Assert.Equal(3, book.AvailableCopies);
        Assert.True(book.IsActive);
        Assert.Equal(now, book.CreatedAtUtc);
        Assert.Equal(now, book.UpdatedAtUtc);
        Assert.Equal(0, book.BorrowedCopies);
    }

    [Fact]
    public void Create_trims_text_fields()
    {
        var book = Book.Create("  Dune  ", "  9780441172719  ", "  Frank Herbert  ", 1, DateTime.UtcNow);

        Assert.Equal("Dune", book.Title);
        Assert.Equal("9780441172719", book.Isbn);
        Assert.Equal("Frank Herbert", book.Author);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_title(string title)
    {
        var exception = Assert.Throws<DomainException>(() =>
            Book.Create(title, "9780441172719", "Frank Herbert", 1, DateTime.UtcNow));

        Assert.Contains("title", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_total_copies_below_one()
    {
        var exception = Assert.Throws<DomainException>(() =>
            Book.Create("Dune", "9780441172719", "Frank Herbert", 0, DateTime.UtcNow));

        Assert.Contains("TotalCopies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateCatalog_does_not_change_isbn()
    {
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 1, DateTime.UtcNow);

        book.UpdateCatalog("Dune Messiah", "Frank Herbert", DateTime.UtcNow);

        Assert.Equal("9780441172719", book.Isbn);
        Assert.Equal("Dune Messiah", book.Title);
    }

    [Fact]
    public void ApplyTotalCopies_rejects_value_below_borrowed_copies()
    {
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 2, DateTime.UtcNow);
        book.ApplyTotalCopies(2, DateTime.UtcNow);
        typeof(Book).GetProperty(nameof(Book.AvailableCopies))!.SetValue(book, 0);

        var exception = Assert.Throws<DomainException>(() => book.ApplyTotalCopies(1, DateTime.UtcNow));

        Assert.Contains("on loan", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, book.TotalCopies);
        Assert.Equal(0, book.AvailableCopies);
    }

    [Fact]
    public void ApplyTotalCopies_raises_available_copies_when_none_are_borrowed()
    {
        var now = DateTime.UtcNow;
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 1, now);

        book.ApplyTotalCopies(4, now.AddMinutes(1));

        Assert.Equal(4, book.TotalCopies);
        Assert.Equal(4, book.AvailableCopies);
    }

    [Fact]
    public void Deactivate_sets_inactive_and_keeps_the_book_retrievable()
    {
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 1, DateTime.UtcNow);

        book.Deactivate(DateTime.UtcNow);

        Assert.False(book.IsActive);
        Assert.Equal("Dune", book.Title);
    }
}
