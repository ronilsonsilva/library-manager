using System.Security.Cryptography;
using System.Text;
using LibraryManager.Application.Loans.CreateLoan;

namespace LibraryManager.UnitTests.Application;

public sealed class IdempotencyCanonicalizationTests
{
    [Fact]
    public void Hash_is_lowercase_sha256_hex_of_canonical_book_and_user_json()
    {
        var bookId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var userId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var json = $"{{\"bookId\":\"{bookId}\",\"userId\":\"{userId}\"}}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        var actual = LoanRequestCanonicalizer.ComputeHash(bookId, userId);

        Assert.Equal(expected, actual);
        Assert.Equal(64, actual.Length);
        Assert.Equal(actual, LoanRequestCanonicalizer.ComputeHash(bookId, userId));
        Assert.NotEqual(actual, LoanRequestCanonicalizer.ComputeHash(userId, bookId));
    }
}
