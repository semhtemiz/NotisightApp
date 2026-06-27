using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Auth.Contracts;

public sealed record LoginRequest(
    [Required]
    string Identifier,

    [Required]
    string Password);
