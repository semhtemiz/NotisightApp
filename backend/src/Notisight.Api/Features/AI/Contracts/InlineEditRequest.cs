using Notisight.Api.Features.Settings.Enums;

namespace Notisight.Api.Features.AI.Contracts;

public sealed class InlineEditRequest
{
    public string Action { get; set; } = string.Empty;
    public string SelectedText { get; set; } = string.Empty;
    public string? SurroundingText { get; set; }
    public string Target { get; set; } = "body";
    public PersonalityTone Tone { get; set; } = PersonalityTone.Casual;
    public ProviderType Provider { get; set; } = ProviderType.DashScope;
    public string? ModelId { get; set; }
}

public sealed record InlineEditResponse(string Result);
