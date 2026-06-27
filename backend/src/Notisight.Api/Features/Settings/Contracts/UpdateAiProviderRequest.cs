using Notisight.Api.Features.Settings.Enums;

namespace Notisight.Api.Features.Settings.Contracts;

public class UpdateAiProviderRequest
{
    public ProviderType ProviderType { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? CustomBaseUrl { get; set; }
}
