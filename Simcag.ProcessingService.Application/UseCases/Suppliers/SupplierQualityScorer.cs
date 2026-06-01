using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.ReadModel.Models;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

internal static class SupplierQualityScorer
{
    internal sealed class PriceAuditAggregate
    {
        public int Count { get; set; }
        public decimal DeviationSum { get; set; }
        public int CriticalEvents { get; set; }
        public int WarningEvents { get; set; }
    }

    internal static Dictionary<Guid, PriceAuditAggregate> AggregatePriceAudits(IEnumerable<SupplierPriceAuditRow> rows)
    {
        var map = new Dictionary<Guid, PriceAuditAggregate>();
        foreach (var row in rows)
        {
            if (!TryParsePriceAudit(row.PayloadJson, out var deviation, out var severity))
                continue;

            if (!map.TryGetValue(row.SupplierId, out var agg))
            {
                agg = new PriceAuditAggregate();
                map[row.SupplierId] = agg;
            }

            agg.Count++;
            agg.DeviationSum += Math.Abs(deviation);

            if (IsCriticalSeverity(severity))
                agg.CriticalEvents++;
            else if (IsWarningSeverity(severity))
                agg.WarningEvents++;
        }

        return map;
    }

    internal static SupplierQualityItemDto Score(
        SupplierExpenseStatsRow supplier,
        PriceAuditAggregate? price,
        SupplierComplianceStatsRow? compliance)
    {
        var expenseCount = supplier.ExpenseCount;
        var openFindings = (compliance?.OpenHighFindings ?? 0)
            + (compliance?.OpenMediumFindings ?? 0)
            + (compliance?.OpenLowFindings ?? 0);

        var reasons = new List<string>();

        if (expenseCount == 0)
        {
            reasons.Add("Sem despesas auditadas ligadas a este fornecedor.");
            return BuildItem(supplier, price, openFindings, null, "DADOS_INSUFICIENTES", "Dados insuficientes", reasons);
        }

        decimal? avgDeviation = null;
        if (price is { Count: > 0 })
            avgDeviation = Math.Round(price.DeviationSum / price.Count, 1);

        if (price is null or { Count: 0 })
            reasons.Add("Ainda sem benchmark de preço (PriceAnalyzed) nas despesas deste fornecedor.");

        var score = 100m;

        if (avgDeviation.HasValue)
        {
            if (avgDeviation.Value <= 10m)
                reasons.Add($"Desvio médio de preço {avgDeviation.Value.ToString(CultureInfo.InvariantCulture)}% — dentro da faixa esperada.");
            else if (avgDeviation.Value <= 25m)
            {
                score -= Math.Min(25m, (avgDeviation.Value - 10m) * 1.5m);
                reasons.Add($"Desvio médio de preço {avgDeviation.Value.ToString(CultureInfo.InvariantCulture)}% — acima do benchmark.");
            }
            else
            {
                score -= Math.Min(45m, 22m + (avgDeviation.Value - 25m) * 2m);
                reasons.Add($"Desvio médio de preço {avgDeviation.Value.ToString(CultureInfo.InvariantCulture)}% — elevado.");
            }
        }

        if (price is { CriticalEvents: > 0 })
        {
            score -= price.CriticalEvents * 18m;
            reasons.Add($"{price.CriticalEvents} evento(s) de preço com severidade crítica.");
        }

        if (price is { WarningEvents: > 0 })
        {
            score -= price.WarningEvents * 6m;
            reasons.Add($"{price.WarningEvents} evento(s) de preço com alerta.");
        }

        if (compliance is { OpenHighFindings: > 0 })
        {
            score -= compliance.OpenHighFindings * 14m;
            reasons.Add($"{compliance.OpenHighFindings} achado(s) de conformidade em aberto (alta/crítica).");
        }

        if (compliance is { OpenMediumFindings: > 0 })
        {
            score -= compliance.OpenMediumFindings * 6m;
            reasons.Add($"{compliance.OpenMediumFindings} achado(s) de conformidade médios em aberto.");
        }

        if (openFindings == 0 && avgDeviation is null or <= 15m && price is { CriticalEvents: 0 })
            reasons.Add("Sem achados críticos de conformidade ou preço.");

        score = Math.Clamp(Math.Round(score, 0), 0m, 100m);

        var tier = ResolveTier(score, expenseCount, avgDeviation, price, compliance);
        var label = TierLabel(tier);

        return BuildItem(supplier, price, openFindings, score, tier, label, reasons);
    }

    private static SupplierQualityItemDto BuildItem(
        SupplierExpenseStatsRow supplier,
        PriceAuditAggregate? price,
        int openFindings,
        decimal? score,
        string tier,
        string tierLabel,
        IReadOnlyList<string> reasons) =>
        new()
        {
            SupplierId = supplier.SupplierId,
            Name = supplier.SupplierName,
            Category = supplier.Category,
            IsActive = supplier.IsActive,
            Tier = tier,
            TierLabel = tierLabel,
            Score = score,
            ExpenseCount = supplier.ExpenseCount,
            TotalSpent = supplier.TotalSpent,
            AvgPriceDeviationPercent = price is { Count: > 0 } ? Math.Round(price.DeviationSum / price.Count, 1) : null,
            PriceAuditCount = price?.Count ?? 0,
            CriticalPriceEvents = price?.CriticalEvents ?? 0,
            WarningPriceEvents = price?.WarningEvents ?? 0,
            OpenComplianceFindings = openFindings,
            Reasons = reasons,
        };

    private static string ResolveTier(
        decimal score,
        int expenseCount,
        decimal? avgDeviation,
        PriceAuditAggregate? price,
        SupplierComplianceStatsRow? compliance)
    {
        if (expenseCount == 0)
            return "DADOS_INSUFICIENTES";

        if ((compliance?.OpenHighFindings ?? 0) > 0 || (price?.CriticalEvents ?? 0) >= 2 || score < 35m)
            return "RISCO";

        if (score >= 75m && (avgDeviation ?? 0m) <= 20m && (compliance?.OpenHighFindings ?? 0) == 0)
            return "RECOMENDADO";

        if (score >= 55m)
            return "ACEITAVEL";

        return "ATENCAO";
    }

    private static string TierLabel(string tier) => tier switch
    {
        "RECOMENDADO" => "Recomendado",
        "ACEITAVEL" => "Aceitável",
        "ATENCAO" => "Atenção",
        "RISCO" => "Risco",
        _ => "Dados insuficientes",
    };

    private static bool TryParsePriceAudit(string? json, out decimal deviation, out string? severity)
    {
        deviation = 0m;
        severity = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("deviationPercentage", out var dev) || dev.ValueKind != JsonValueKind.Number)
                return false;

            deviation = dev.GetDecimal();
            if (root.TryGetProperty("severity", out var sev) && sev.ValueKind == JsonValueKind.String)
                severity = sev.GetString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCriticalSeverity(string? severity) =>
        string.Equals(severity, "CRITICAL", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningSeverity(string? severity) =>
        string.Equals(severity, "WARNING", StringComparison.OrdinalIgnoreCase);
}
