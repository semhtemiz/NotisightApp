namespace Notisight.Api.Options;

public sealed class DeepgramOptions
{
    public const string SectionName = "Deepgram";

    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.deepgram.com/v1/listen";
    public string Model { get; set; } = "nova-3";
    public string Language { get; set; } = "tr";
    public bool SmartFormat { get; set; } = true;
}
