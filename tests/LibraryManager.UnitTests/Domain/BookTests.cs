using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class BookTests
{
    [Fact]
    public void Create_sets_available_copies_equal_to_total_and_is_active()
    {
        var now = DateTime.UtcNow;

        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 3, now).Value;

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
        var book = Book.Create("  Dune  ", "  9780441172719  ", "  Frank Herbert  ", 1, DateTime.UtcNow).Value;

        Assert.Equal("Dune", book.Title);
        Assert.Equal("9780441172719", book.Isbn);
        Assert.Equal("Frank Herbert", book.Author);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_title(string title)
    {
        var result = Book.Create(title, "9780441172719", "Frank Herbert", 1, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTitleRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_rejects_total_copies_below_one()
    {
        var result = Book.Create("Dune", "9780441172719", "Frank Herbert", 0, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTotalCopiesInvalid, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void UpdateCatalog_does_not_change_isbn()
    {
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 1, DateTime.UtcNow).Value;

        var updated = book.UpdateCatalog("Dune Messiah", "Frank Herbert", DateTime.UtcNow);

        Assert.True(updated.IsSuccess);
        Assert.Equal("9780441172719", book.Isbn);
        Assert.Equal("Dune Messiah", book.Title);
    }

    [Fact]
    public void ApplyTotalCopies_rejects_value_below_borrowed_copies()
    {
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 2, DateTime.UtcNow).Value;
        Assert.True(book.ApplyTotalCopies(2, DateTime.UtcNow).IsSuccess);
        typeof(Book).GetProperty(nameof(Book.AvailableCopies))!.SetValue(book, 0);

        var result = book.ApplyTotalCopies(1, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTotalCopiesBelowBorrowed, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(2, book.TotalCopies);
        Assert.Equal(0, book.AvailableCopies);
    }

    [Fact]
    public void ApplyTotalCopies_raises_available_copies_when_none_are_borrowed()
    {
        var now = DateTime.UtcNow;
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 1, now).Value;

        Assert.True(book.ApplyTotalCopies(4, now.AddMinutes(1)).IsSuccess);

        Assert.Equal(4, book.TotalCopies);
        Assert.Equal(4, book.AvailableCopies);
    }

    [Fact]
    public void Deactivate_sets_inactive_and_keeps_the_book_retrievable()
    {
        var book = Book.Create("Dune", "9780441172719", "Frank Herbert", 1, DateTime.UtcNow).Value;

        book.Deactivate(DateTime.UtcNow);

        Assert.False(book.IsActive);
        Assert.Equal("Dune", book.Title);
    }
}
