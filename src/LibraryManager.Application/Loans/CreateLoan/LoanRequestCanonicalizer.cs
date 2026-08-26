using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LibraryManager.Application.Loans.CreateLoan;

public static class LoanRequestCanonicalizer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ComputeHash(Guid bookId, Guid userId)
    {
        var json = JsonSerializer.Serialize(new CanonicalLoanRequest(bookId, userId), Options);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record CanonicalLoanRequest(Guid BookId, Guid UserId);
}
