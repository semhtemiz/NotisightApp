using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Auth.Contracts;

public sealed record UpdateProfileRequest(
    [Required]
    [MinLength(3)]
    [MaxLength(60)]
    string DisplayName,

    [Required]
    [MinLength(3)]
    [MaxLength(60)]
    [RegularExpression(
        @"^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Username can contain only letters, numbers, dots, underscores, and hyphens.")]
    string Username,

    [Required]
    [EmailAddress]
    string Email);
