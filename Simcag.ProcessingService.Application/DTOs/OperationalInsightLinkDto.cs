namespace Simcag.ProcessingService.Application.DTOs;

/// <summary>Ligação acionável na UI (rotas do portal ou URLs externas).</summary>
public sealed class OperationalInsightLinkDto
{
    public string Label { get; init; } = "";
    public string Href { get; init; } = "";
}
