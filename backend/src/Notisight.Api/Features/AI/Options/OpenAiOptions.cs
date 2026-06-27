namespace Notisight.Api.Features.AI.Options;

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = string.Empty;
}
