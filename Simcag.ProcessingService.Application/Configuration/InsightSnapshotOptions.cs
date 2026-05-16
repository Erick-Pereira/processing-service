namespace Simcag.ProcessingService.Application.Configuration;

public sealed class InsightSnapshotOptions
{
    public const string SectionName = "InsightSnapshot";

    /// <summary>Bump quando limiares ou kinds de insight mudarem (invalida snapshots antigos).</summary>
    public string RuleSetVersion { get; set; } = "insights-v2-20260515";

    public int TtlMinutes { get; set; } = 60;

    public int HistoryTakeMax { get; set; } = 60;
}
