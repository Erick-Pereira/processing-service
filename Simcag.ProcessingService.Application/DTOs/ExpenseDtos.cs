using System;
using System.Collections.Generic;

namespace Simcag.ProcessingService.Application.DTOs;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Total { get; }
    public int Page { get; }
    public int PageSize { get; }

    public PagedResult(IReadOnlyList<T> items, int total, int page, int pageSize)
    {
        Items = items;
        Total = total;
        Page = page;
        PageSize = pageSize;
    }
}

public class ExpenseListItemDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }

    /// <summary>Espelho legado (Pending, Approved, Paid, Cancelled, Rejected).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Pipeline técnica (Received, Persisting, Completed, Benchmarking, …).</summary>
    public string ProcessingStatus { get; set; } = string.Empty;

    public string ApprovalStatus { get; set; } = string.Empty;
    public string SettlementStatus { get; set; } = string.Empty;
    public string? ProcessingFailureReason { get; set; }
    public DateTime? LastPipelineTransitionAt { get; set; }

    /// <summary>0–1 quando disponível (ingestão / IA).</summary>
    public decimal? ConfidenceScore { get; set; }

    public bool LowConfidence { get; set; }

    /// <summary>Nome do fornecedor (join na listagem).</summary>
    public string? SupplierName { get; set; }

    public string Currency { get; set; } = "BRL";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ExpenseDetailDto : ExpenseListItemDto
{
    public DateTime? ProcessingFailedAt { get; set; }
    public int ProcessingRetryCount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? RawDocumentId { get; set; }
    public IReadOnlyList<ExpenseItemDto> Items { get; set; } = Array.Empty<ExpenseItemDto>();
    public IReadOnlyList<PaymentDto> Payments { get; set; } = Array.Empty<PaymentDto>();

    /// <summary>Eventos operacionais (marcos + auditoria) ordenados por tempo.</summary>
    public IReadOnlyList<ExpenseOperationalTimelineEntryDto> OperationalTimeline { get; set; } =
        Array.Empty<ExpenseOperationalTimelineEntryDto>();

    public ExpenseGovernanceDto? Governance { get; set; }

    /// <summary>Análises de preço/benchmark extraídas da auditoria (PriceAnalyzed).</summary>
    public IReadOnlyList<ExpensePriceAnalysisDto> PriceAnalyses { get; set; } =
        Array.Empty<ExpensePriceAnalysisDto>();
}

public sealed class ExpenseOperationalTimelineEntryDto
{
    /// <summary><c>system</c> ou <c>audit</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime At { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Action { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
}

public sealed class ExpenseGovernanceDto
{
    public IReadOnlyList<ExpensePipelineStepDto> ProcessingSteps { get; set; } = Array.Empty<ExpensePipelineStepDto>();
    public IReadOnlyList<ExpensePipelineStepDto> ApprovalSteps { get; set; } = Array.Empty<ExpensePipelineStepDto>();
    public IReadOnlyList<ExpensePipelineStepDto> SettlementSteps { get; set; } = Array.Empty<ExpensePipelineStepDto>();
    public IReadOnlyList<string> NextActions { get; set; } = Array.Empty<string>();
    public ExpenseAllowedActionsDto AllowedActions { get; set; } = new();
}

public sealed class ExpensePipelineStepDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary><c>current</c>, <c>done</c>, <c>pending</c>, <c>skipped</c>, <c>catalog</c> (catálogo de estados técnicos).</summary>
    public string State { get; set; } = string.Empty;
}

public sealed class ExpenseAllowedActionsDto
{
    public bool Approve { get; set; }
    public bool Reject { get; set; }
    public bool Cancel { get; set; }
    public bool RetryProcessing { get; set; }
    public bool RegisterPayment { get; set; }
    public bool RefundPayment { get; set; }

    /// <summary>Motivos quando a ação correspondente está desabilitada (chaves camelCase).</summary>
    public IReadOnlyDictionary<string, string> DisabledReasons { get; set; } =
        new Dictionary<string, string>();
}

public sealed class ExpensePriceAnalysisDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public int? Quantity { get; set; }
    public decimal? LineTotal { get; set; }
    public decimal MarketAverage { get; set; }
    public decimal HistoricalAverage { get; set; }
    public decimal DeviationPercentage { get; set; }
    public string Severity { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string MarketSource { get; set; } = string.Empty;
    public string MarketBenchmarkKind { get; set; } = string.Empty;
    public string MarketBenchmarkStatus { get; set; } = string.Empty;
    public string MarketConfidence { get; set; } = string.Empty;
    public int? MarketSampleCount { get; set; }
    public decimal? MarketRelativeSpread { get; set; }
    public string MarketSearchQuery { get; set; } = string.Empty;
    public decimal? MarketDocumentAnchorPrice { get; set; }
    public IReadOnlyList<ExpenseMarketEvidenceDto> MarketEvidence { get; set; } =
        Array.Empty<ExpenseMarketEvidenceDto>();
    public IReadOnlyList<ExpenseMarketReferenceLinkDto> MarketReferenceLinks { get; set; } =
        Array.Empty<ExpenseMarketReferenceLinkDto>();
    public IReadOnlyList<ExpenseMarketPriceSampleDto> MarketSamples { get; set; } =
        Array.Empty<ExpenseMarketPriceSampleDto>();
    public decimal? NfUnitPrice { get; set; }
    public int? NfQuantity { get; set; }
    public decimal? NfLineTotal { get; set; }
    public bool PriceAuditCorrected { get; set; }
}

public sealed class ExpenseMarketPriceSampleDto
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? PriceBrl { get; set; }
    public string? Provider { get; set; }
}

public sealed class ExpenseMarketReferenceLinkDto
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class ExpenseMarketEvidenceDto
{
    public string Scope { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public sealed class ExpenseItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public sealed class PaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? ReferenceCode { get; set; }
    public bool IsRefunded { get; set; }
    public DateTime? RefundedAt { get; set; }
}

public sealed class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
