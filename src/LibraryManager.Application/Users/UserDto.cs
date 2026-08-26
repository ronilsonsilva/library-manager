using LibraryManager.Domain;

namespace LibraryManager.Application.Users;

public sealed record UserDto(Guid Id, string Name, string Email, DateTime CreatedAtUtc)
{
    public static UserDto From(User user) => new(user.Id, user.Name, user.Email, user.CreatedAtUtc);
}
