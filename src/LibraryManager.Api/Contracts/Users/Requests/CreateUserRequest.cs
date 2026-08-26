using System.ComponentModel.DataAnnotations;
using LibraryManager.Domain;

namespace LibraryManager.Api.Contracts.Users.Requests;

public sealed record CreateUserRequest(
    [Required]
    [StringLength(User.NameMaxLength)]
    string Name,
    [Required]
    [StringLength(User.EmailMaxLength)]
    string Email);
