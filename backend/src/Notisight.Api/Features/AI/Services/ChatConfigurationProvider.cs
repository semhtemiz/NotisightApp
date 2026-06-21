using Notisight.Api.Features.Settings.Enums;

namespace Notisight.Api.Features.AI.Services;

public class ChatConfiguration
{
    public ProviderType ProviderType { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? CustomBaseUrl { get; set; }
}

public interface IChatConfigurationProvider
{
    ChatConfiguration? Current { get; }
    void SetConfiguration(ChatConfiguration configuration);
}

public class ChatConfigurationProvider : IChatConfigurationProvider
{
    public ChatConfiguration? Current { get; private set; }

    public void SetConfiguration(ChatConfiguration configuration)
    {
        Current = configuration;
    }
}
