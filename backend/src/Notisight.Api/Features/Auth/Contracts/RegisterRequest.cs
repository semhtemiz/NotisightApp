using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Auth.Contracts;

public sealed record RegisterRequest(
    [Required]
    [MinLength(3)]
    [MaxLength(60)]
    [RegularExpression(
        @"^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Username can contain only letters, numbers, dots, underscores, and hyphens.")]
    string Username,

    [Required]
    [EmailAddress]
    string Email,

    [Required]
    [MinLength(8)]
    [RegularExpression(
        @"^(?=.*[a-zçğıöşü])(?=.*[A-ZÇĞİÖŞÜ])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    string Password);
