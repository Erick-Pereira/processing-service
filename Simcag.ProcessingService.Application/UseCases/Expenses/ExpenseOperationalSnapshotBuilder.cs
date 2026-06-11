using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>
/// Vista operacional derivada do agregado + auditoria (sem alterar o domínio).
/// </summary>
public static class ExpenseOperationalSnapshotBuilder
{
    public static ExpenseGovernanceDto BuildGovernance(Expense e)
    {
        var processing = BuildProcessingCatalog(e);
        var approval = BuildApprovalTrack(e);
        var settlement = BuildSettlementTrack(e);
        var flags = BuildAllowedActions(e);
        var next = new List<string>();
        if (flags.Approve) next.Add("approve");
        if (flags.Reject) next.Add("reject");
        if (flags.Cancel) next.Add("cancel");
        if (flags.RetryProcessing) next.Add("retry_processing");
        if (flags.RegisterPayment) next.Add("register_payment");
        if (flags.RefundPayment) next.Add("refund_payment");
        return new ExpenseGovernanceDto
        {
            ProcessingSteps = processing,
            ApprovalSteps = approval,
            SettlementSteps = settlement,
            NextActions = next,
            AllowedActions = flags,
        };
    }

    public static IReadOnlyList<ExpenseOperationalTimelineEntryDto> BuildTimeline(
        Expense e,
        IReadOnlyList<AuditLog> auditsChronological)
    {
        var list = new List<ExpenseOperationalTimelineEntryDto>
        {
            new()
            {
                Source = "system",
                At = e.CreatedAt,
                Title = "Despesa criada",
                Detail = e.RawDocumentId.HasValue
                    ? $"Associada ao documento {e.RawDocumentId}"
                    : "Registo manual ou consolidado.",
                Action = "Created",
            },
        };

        if (e.ConfidenceScore.HasValue)
        {
            list.Add(new ExpenseOperationalTimelineEntryDto
            {
                Source = "system",
                At = e.CreatedAt,
                Title = "Confiança do enriquecimento",
                Detail = $"Score {(e.ConfidenceScore.Value * 100m):0.#}%{(e.LowConfidence ? " (baixa confiança)" : "")}",
                Action = "Confidence",
            });
        }

        foreach (var a in auditsChronological)
        {
            var (title, detail) = DescribeAuditEntry(a);
            list.Add(new ExpenseOperationalTimelineEntryDto
            {
                Source = "audit",
                At = a.CreatedAt,
                Title = title,
                Detail = detail,
                Action = a.Action,
                ActorId = a.PerformedBy,
                ActorName = a.PerformedByName,
            });
        }

        if (e.ProcessingFailedAt.HasValue)
        {
            list.Add(new ExpenseOperationalTimelineEntryDto
            {
                Source = "system",
                At = e.ProcessingFailedAt.Value,
                Title = "Falha de processamento",
                Detail = e.ProcessingFailureReason,
                Action = "ProcessingFailed",
            });
        }

        return list.OrderByDescending(x => x.At).ThenByDescending(x => x.Source == "audit" ? 1 : 0).ToList();
    }

    private static (string Title, string? Detail) DescribeAuditEntry(AuditLog a) =>
        a.Action switch
        {
            "PriceAnalyzed" => ("Análise de preço / benchmark", SummarizePriceAnalyzed(a.NewValue)),
            "Insert" => ("Registo persistido", Truncate(a.NewValue, 400)),
            "Update" => ("Atualização", Truncate(a.NewValue, 400)),
            "Delete" => ("Remoção", null),
            _ => ($"Auditoria: {a.Action}", Truncate(a.NewValue ?? a.OldValue, 400)),
        };

    private static string? SummarizePriceAnalyzed(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var parts = new List<string>();
            if (r.TryGetProperty("productName", out var pn))
                parts.Add($"Produto: {pn.GetString()}");
            if (r.TryGetProperty("deviationPercentage", out var dev) && dev.ValueKind == JsonValueKind.Number)
                parts.Add($"Desvio: {dev.GetDecimal():0.#}%");
            if (r.TryGetProperty("marketAverage", out var mkt) && mkt.ValueKind == JsonValueKind.Number && mkt.GetDecimal() > 0m)
                parts.Add($"Mercado: R$ {mkt.GetDecimal():0.##}");
            if (r.TryGetProperty("processingAfter", out var pa))
                parts.Add($"Pipeline → {pa.GetString()}");
            return parts.Count == 0 ? Truncate(json, 300) : string.Join(" · ", parts);
        }
        catch
        {
            return Truncate(json, 300);
        }
    }

    private static string? Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var t = s.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }

    /// <summary>Catálogo linear da pipeline — só etapas percorridas + atual (sem listar todo o enum).</summary>
    private static IReadOnlyList<ExpensePipelineStepDto> BuildProcessingCatalog(Expense e)
    {
        if (e.ProcessingStatus == ExpenseProcessingStatus.Failed)
        {
            return
            [
                new ExpensePipelineStepDto
                {
                    Code = nameof(ExpenseProcessingStatus.Failed),
                    Label = HumanizeProcessing(ExpenseProcessingStatus.Failed),
                    State = "current",
                },
            ];
        }

        if (e.ProcessingStatus == ExpenseProcessingStatus.PartiallyCompleted)
        {
            return
            [
                new ExpensePipelineStepDto
                {
                    Code = nameof(ExpenseProcessingStatus.PartiallyCompleted),
                    Label = HumanizeProcessing(ExpenseProcessingStatus.PartiallyCompleted),
                    State = "current",
                },
            ];
        }

        ExpenseProcessingStatus[] order =
        [
            ExpenseProcessingStatus.Received,
            ExpenseProcessingStatus.Enriching,
            ExpenseProcessingStatus.Persisting,
            ExpenseProcessingStatus.Completed,
            ExpenseProcessingStatus.Benchmarking,
        ];

        var curIdx = Array.IndexOf(order, e.ProcessingStatus);
        if (curIdx < 0)
        {
            return
            [
                new ExpensePipelineStepDto
                {
                    Code = e.ProcessingStatus.ToString(),
                    Label = HumanizeProcessing(e.ProcessingStatus),
                    State = "current",
                },
            ];
        }

        var steps = new List<ExpensePipelineStepDto>(curIdx + 1);
        for (var i = 0; i <= curIdx; i++)
        {
            var s = order[i];
            steps.Add(new ExpensePipelineStepDto
            {
                Code = s.ToString(),
                Label = HumanizeProcessing(s),
                State = i == curIdx ? "current" : "done",
            });
        }

        return steps;
    }

    private static string HumanizeProcessing(ExpenseProcessingStatus s) =>
        s switch
        {
            ExpenseProcessingStatus.Received => "Recebido / ingestão",
            ExpenseProcessingStatus.Enriching => "Enriquecimento (IA)",
            ExpenseProcessingStatus.Benchmarking => "Benchmark de mercado",
            ExpenseProcessingStatus.Persisting => "Persistência de itens",
            ExpenseProcessingStatus.Completed => "Processamento concluído",
            ExpenseProcessingStatus.Failed => "Falha técnica",
            ExpenseProcessingStatus.PartiallyCompleted => "Concluído com lacunas",
            _ => s.ToString(),
        };

    private static IReadOnlyList<ExpensePipelineStepDto> BuildApprovalTrack(Expense e)
    {
        var cur = e.ApprovalStatus;
        var outcomeLabel = cur switch
        {
            ExpenseApprovalStatus.Approved => "Resultado: aprovada",
            ExpenseApprovalStatus.Rejected => "Resultado: rejeitada",
            ExpenseApprovalStatus.Cancelled => "Resultado: cancelada",
            _ => "Resultado: pendente",
        };
        return new List<ExpensePipelineStepDto>
        {
            new()
            {
                Code = nameof(ExpenseApprovalStatus.PendingApproval),
                Label = "Decisão humana (síndico / conselho)",
                State = cur == ExpenseApprovalStatus.PendingApproval ? "current" : "done",
            },
            new()
            {
                Code = "Outcome",
                Label = outcomeLabel,
                State = cur == ExpenseApprovalStatus.PendingApproval ? "pending" : "current",
            },
        };
    }

    private static IReadOnlyList<ExpensePipelineStepDto> BuildSettlementTrack(Expense e)
    {
        var s = e.SettlementStatus;
        return new List<ExpensePipelineStepDto>
        {
            new()
            {
                Code = nameof(ExpenseSettlementStatus.Unpaid),
                Label = "Em aberto (sem pagamentos)",
                State = s == ExpenseSettlementStatus.Unpaid ? "current" : "done",
            },
            new()
            {
                Code = nameof(ExpenseSettlementStatus.PartiallyPaid),
                Label = "Parcialmente paga",
                State = s == ExpenseSettlementStatus.PartiallyPaid
                    ? "current"
                    : s == ExpenseSettlementStatus.Paid
                        ? "done"
                        : "pending",
            },
            new()
            {
                Code = nameof(ExpenseSettlementStatus.Paid),
                Label = "Totalmente liquidada",
                State = s == ExpenseSettlementStatus.Paid ? "current" : "pending",
            },
        };
    }

    private static ExpenseAllowedActionsDto BuildAllowedActions(Expense e)
    {
        var hasActivePayments = e.Payments.Any(p => !p.IsRefunded);
        var reasons = new Dictionary<string, string>();

        var canApprove = e.ApprovalStatus == ExpenseApprovalStatus.PendingApproval
                         && e.ProcessingStatus is ExpenseProcessingStatus.Completed or ExpenseProcessingStatus.PartiallyCompleted
                         && e.ProcessingStatus != ExpenseProcessingStatus.Failed
                         && e.Items.Count > 0;
        if (!canApprove && e.ApprovalStatus == ExpenseApprovalStatus.PendingApproval)
        {
            if (e.ProcessingStatus == ExpenseProcessingStatus.Failed)
                reasons["approve"] = "Processamento falhou — reinicie a pipeline ou cancele.";
            else if (e.Items.Count == 0)
                reasons["approve"] = "Adicione itens à despesa antes de aprovar.";
            else if (e.ProcessingStatus is not (ExpenseProcessingStatus.Completed or ExpenseProcessingStatus.PartiallyCompleted))
                reasons["approve"] = "Aguarde o processamento automático concluir.";
        }

        var canReject = e.ApprovalStatus == ExpenseApprovalStatus.PendingApproval;
        if (!canReject)
            reasons["reject"] = "Disponível apenas enquanto a aprovação estiver pendente.";

        var canCancel = e.SettlementStatus != ExpenseSettlementStatus.Paid
                        && e.ApprovalStatus != ExpenseApprovalStatus.Cancelled
                        && !hasActivePayments;
        if (!canCancel)
        {
            if (e.ApprovalStatus == ExpenseApprovalStatus.Cancelled)
                reasons["cancel"] = "Despesa já cancelada.";
            else if (e.SettlementStatus == ExpenseSettlementStatus.Paid)
                reasons["cancel"] = "Nota totalmente liquidada — use estorno em vez de cancelamento.";
            else if (hasActivePayments)
                reasons["cancel"] = "Estorne os pagamentos ativos antes de cancelar a nota.";
        }

        var canRetry = e.ProcessingStatus == ExpenseProcessingStatus.Failed
                       && e.ApprovalStatus != ExpenseApprovalStatus.Approved
                       && !e.IsClosedForPipeline();
        if (!canRetry)
        {
            if (e.IsClosedForPipeline())
                reasons["retryProcessing"] = "Despesa encerrada (cancelada ou rejeitada).";
            else if (e.ApprovalStatus == ExpenseApprovalStatus.Approved)
                reasons["retryProcessing"] = "Despesa já aprovada — retry não permitido.";
            else if (e.ProcessingStatus != ExpenseProcessingStatus.Failed)
                reasons["retryProcessing"] = "Reinício disponível somente após falha técnica (Failed).";
        }

        var canPay = e.ApprovalStatus == ExpenseApprovalStatus.Approved && e.OutstandingBalance > 0.001m;
        if (!canPay && e.ApprovalStatus == ExpenseApprovalStatus.Approved)
            reasons["registerPayment"] = "Saldo em aberto zerado ou despesa encerrada.";

        var canRefund = e.ApprovalStatus == ExpenseApprovalStatus.Approved
                        && e.Payments.Any(p => !p.IsRefunded);

        return new ExpenseAllowedActionsDto
        {
            Approve = canApprove,
            Reject = canReject,
            Cancel = canCancel,
            RetryProcessing = canRetry,
            RegisterPayment = canPay,
            RefundPayment = canRefund,
            DisabledReasons = reasons,
        };
    }
}
