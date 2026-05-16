namespace Simcag.ProcessingService.Application.DTOs;

/// <summary>Metadados de snapshots passados (sem payload completo — use auditoria ou export dedicado).</summary>
public sealed class OperationalInsightSnapshotSummaryDto
{
    public Guid Id { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string RuleSetVersion { get; init; } = "";
}
