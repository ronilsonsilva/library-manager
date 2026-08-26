using System.ComponentModel.DataAnnotations;
using LibraryManager.Domain;

namespace LibraryManager.Api.Contracts.Books.Requests;

public sealed record UpdateBookRequest(
    [Required]
    [StringLength(Book.TitleMaxLength)]
    string Title,
    [Required]
    [StringLength(Book.AuthorMaxLength)]
    string Author,
    [Range(1, int.MaxValue)]
    int TotalCopies);
