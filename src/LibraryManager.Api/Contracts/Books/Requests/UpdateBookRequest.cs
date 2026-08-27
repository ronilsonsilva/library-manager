using System.ComponentModel.DataAnnotations;
using LibraryManager.Domain;

namespace LibraryManager.Api.Contracts.Books.Requests;

public sealed record UpdateBookRequest(
    [Required(ErrorMessage = "Validation_Title_Required")]
    [StringLength(Book.TitleMaxLength, ErrorMessage = "Validation_Title_MaxLength")]
    string Title,
    [Required(ErrorMessage = "Validation_Author_Required")]
    [StringLength(Book.AuthorMaxLength, ErrorMessage = "Validation_Author_MaxLength")]
    string Author,
    [Range(1, int.MaxValue, ErrorMessage = "Validation_TotalCopies_Range")]
    int TotalCopies);
