using Notisight.Api.Features.Settings.Enums;

namespace Notisight.Api.Features.Settings.Contracts;

public class AiProviderDto
{
    public ProviderType ProviderType { get; set; }
    public bool IsConfigured { get; set; }
    public string? MaskedApiKey { get; set; }
    public string? CustomBaseUrl { get; set; }
}
