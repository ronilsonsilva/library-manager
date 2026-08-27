using System.ComponentModel.DataAnnotations;
using LibraryManager.Domain;

namespace LibraryManager.Api.Contracts.Users.Requests;

public sealed record CreateUserRequest(
    [Required(ErrorMessage = "Validation_Name_Required")]
    [StringLength(User.NameMaxLength, ErrorMessage = "Validation_Name_MaxLength")]
    string Name,
    [Required(ErrorMessage = "Validation_Email_Required")]
    [StringLength(User.EmailMaxLength, ErrorMessage = "Validation_Email_MaxLength")]
    string Email);
