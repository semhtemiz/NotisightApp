using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Tags.Contracts;

public sealed record TagRequest(
    [Required]
    [MaxLength(80)]
    string Name);
