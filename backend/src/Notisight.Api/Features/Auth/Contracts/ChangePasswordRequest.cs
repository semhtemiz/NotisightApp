using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Auth.Contracts;

public sealed record ChangePasswordRequest(
    [Required]
    string CurrentPassword,

    [Required]
    [MinLength(8)]
    [RegularExpression(
        @"^(?=.*[a-zçğıöşü])(?=.*[A-ZÇĞİÖŞÜ])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    string NewPassword);
