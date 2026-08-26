using LibraryManager.Application.Users;

namespace LibraryManager.Api.Contracts.Users.Responses;

public sealed record UserResponse(Guid Id, string Name, string Email, DateTime CreatedAtUtc)
{
    public static UserResponse From(UserDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new(dto.Id, dto.Name, dto.Email, dto.CreatedAtUtc);
    }
}
