using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Application.Compliance;

public sealed record ComplianceCandidate(
    string RuleCode,
    string Title,
    string Description,
    string Severity,
    string Status,
    string Origin,
    decimal? Confidence,
    string? DetailJson);

/// <summary>
/// Motor declarativo de regras (MVP). Evolui para catálogo persistido / DSL.
/// </summary>
public static class ExpenseComplianceEvaluator
{
    public static IReadOnlyList<ComplianceCandidate> Evaluate(Expense expense, IReadOnlyList<AuditLog> auditsNewestFirst)
    {
        var list = new List<ComplianceCandidate>();
        var now = DateTime.UtcNow;

        // --- Confiança / IA ---
        var lowConf = expense.LowConfidence
            || (expense.ConfidenceScore is { } cs && cs < 0.60m);
        list.Add(
            lowConf
                ? new ComplianceCandidate(
                    "LOW_CONFIDENCE",
                    "Revisão de confiança (IA / enriquecimento)",
                    "A despesa está marcada com baixa confiança ou score abaixo do limiar operacional (0,60).",
                    "HIGH",
                    "OUTSTANDING",
                    expense.LowConfidence ? "AI" : "RULE",
                    expense.ConfidenceScore,
                    JsonSerializer.Serialize(new { threshold = 0.60m, lowConfidenceFlag = expense.LowConfidence }))
                : new ComplianceCandidate(
                    "LOW_CONFIDENCE",
                    "Confiança do enriquecimento",
                    "Score dentro do limiar ou não aplicável.",
                    "LOW",
                    "CLEAR",
                    "RULE",
                    expense.ConfidenceScore,
                    null));

        // --- Pipeline ---
        if (expense.ProcessingStatus == ExpenseProcessingStatus.Failed)
        {
            list.Add(new ComplianceCandidate(
                "PIPELINE_FAILED",
                "Falha técnica da pipeline",
                expense.ProcessingFailureReason ?? "Processamento falhou — intervenção ou retry necessário.",
                "CRITICAL",
                "OUTSTANDING",
                "RULE",
                null,
                JsonSerializer.Serialize(new { processingStatus = expense.ProcessingStatus.ToString() })));
        }
        else if (expense.ProcessingStatus is not (ExpenseProcessingStatus.Completed or ExpenseProcessingStatus.PartiallyCompleted))
        {
            list.Add(new ComplianceCandidate(
                "PIPELINE_INCOMPLETE",
                "Pipeline automática incompleta",
                $"Estado técnico atual: {expense.ProcessingStatus}. Aguardar conclusão antes de decisões finais.",
                "MEDIUM",
                "OUTSTANDING",
                "RULE",
                null,
                JsonSerializer.Serialize(new { processingStatus = expense.ProcessingStatus.ToString() })));
        }
        else
        {
            list.Add(new ComplianceCandidate(
                "PIPELINE_HEALTH",
                "Pipeline técnica",
                "Processamento concluído (com ou sem lacunas controladas).",
                "LOW",
                "CLEAR",
                "RULE",
                null,
                JsonSerializer.Serialize(new { processingStatus = expense.ProcessingStatus.ToString() })));
        }

        // --- Aprovação parada (SLA simples) ---
        if (expense.ApprovalStatus == ExpenseApprovalStatus.PendingApproval)
        {
            var days = (now - expense.IssueDate.ToUniversalTime()).TotalDays;
            var stalled = days > 14;
            list.Add(
                stalled
                    ? new ComplianceCandidate(
                        "APPROVAL_SLA",
                        "Aprovação humana fora do prazo operacional",
                        $"Em aprovação há mais de 14 dias desde a emissão ({days:0.#} dias).",
                        "MEDIUM",
                        "OUTSTANDING",
                        "RULE",
                        null,
                        JsonSerializer.Serialize(new { daysOpen = days, slaDays = 14 }))
                    : new ComplianceCandidate(
                        "APPROVAL_SLA",
                        "Aprovação humana dentro do prazo",
                        "Pendência de aprovação dentro do SLA operacional de 14 dias.",
                        "LOW",
                        "CLEAR",
                        "RULE",
                        null,
                        JsonSerializer.Serialize(new { daysOpen = days, slaDays = 14 })));
        }
        else
        {
            list.Add(new ComplianceCandidate(
                "APPROVAL_SLA",
                "Decisão de aprovação",
                $"Estado: {expense.ApprovalStatus}.",
                "LOW",
                "CLEAR",
                "RULE",
                null,
                JsonSerializer.Serialize(new { approvalStatus = expense.ApprovalStatus.ToString() })));
        }

        // --- Benchmark (auditoria PriceAnalyzed) ---
        var deviation = TryMaxPriceDeviationPercent(auditsNewestFirst);
        if (deviation.HasValue)
        {
            var hot = deviation.Value > 25m;
            list.Add(
                hot
                    ? new ComplianceCandidate(
                        "BENCHMARK_DEVIATION",
                        "Desvio de preço vs mercado",
                        $"Desvio máximo observado nas análises de preço: {deviation.Value:0.#}% (limiar operacional 25%).",
                        "HIGH",
                        "OUTSTANDING",
                        "AI",
                        null,
                        JsonSerializer.Serialize(new { maxDeviationPercent = deviation.Value, thresholdPercent = 25m }))
                    : new ComplianceCandidate(
                        "BENCHMARK_DEVIATION",
                        "Benchmark de mercado",
                        $"Desvio máximo {deviation.Value:0.#}% dentro do limiar.",
                        "LOW",
                        "CLEAR",
                        "AI",
                        null,
                        JsonSerializer.Serialize(new { maxDeviationPercent = deviation.Value, thresholdPercent = 25m })));
        }
        else
        {
            list.Add(new ComplianceCandidate(
                "BENCHMARK_DEVIATION",
                "Benchmark de mercado",
                "Sem evento de análise de preço correlacionado — sem avaliação automática de desvio.",
                "LOW",
                "CLEAR",
                "RULE",
                null,
                null));
        }

        // --- Documento de origem ---
        if (!expense.RawDocumentId.HasValue && expense.ProcessingStatus == ExpenseProcessingStatus.Completed)
        {
            list.Add(new ComplianceCandidate(
                "SOURCE_DOCUMENT",
                "Documento de origem",
                "Despesa sem RawDocumentId associado — rastreabilidade documental reduzida.",
                "MEDIUM",
                "OUTSTANDING",
                "RULE",
                null,
                null));
        }
        else
        {
            list.Add(new ComplianceCandidate(
                "SOURCE_DOCUMENT",
                "Documento de origem",
                expense.RawDocumentId.HasValue
                    ? $"Associada ao documento {expense.RawDocumentId}."
                    : "Documento ainda não consolidado ou ingestão em curso.",
                "LOW",
                expense.RawDocumentId.HasValue ? "CLEAR" : "OUTSTANDING",
                "RULE",
                null,
                JsonSerializer.Serialize(new { rawDocumentId = expense.RawDocumentId })));
        }

        return list;
    }

    private static decimal? TryMaxPriceDeviationPercent(IReadOnlyList<AuditLog> auditsNewestFirst)
    {
        decimal? max = null;
        foreach (var a in auditsNewestFirst)
        {
            if (!string.Equals(a.Action, "PriceAnalyzed", StringComparison.OrdinalIgnoreCase))
                continue;
            var json = a.NewValue ?? a.OldValue;
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("deviationPercentage", out var dev))
                    continue;
                if (dev.ValueKind != JsonValueKind.Number) continue;
                var v = dev.GetDecimal();
                var abs = Math.Abs(v);
                max = max.HasValue ? Math.Max(max.Value, abs) : abs;
            }
            catch
            {
                // ignore malformed audit payloads
            }
        }

        return max;
    }
}
