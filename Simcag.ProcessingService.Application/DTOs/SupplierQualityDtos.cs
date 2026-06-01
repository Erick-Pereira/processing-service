using System;
using System.Collections.Generic;

namespace Simcag.ProcessingService.Application.DTOs;

public sealed class SupplierQualityAnalysisDto
{
    public IReadOnlyList<SupplierQualityItemDto> Suppliers { get; init; } = Array.Empty<SupplierQualityItemDto>();
    public SupplierQualitySummaryDto Summary { get; init; } = new();
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class SupplierQualitySummaryDto
{
    public int TotalSuppliers { get; init; }
    public int RecommendedCount { get; init; }
    public int AcceptableCount { get; init; }
    public int AttentionCount { get; init; }
    public int HighRiskCount { get; init; }
    public int InsufficientDataCount { get; init; }
}

public sealed class SupplierQualityItemDto
{
    public Guid SupplierId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public bool IsActive { get; init; }

    /// <summary>RECOMENDADO | ACEITAVEL | ATENCAO | RISCO | DADOS_INSUFICIENTES</summary>
    public string Tier { get; init; } = "DADOS_INSUFICIENTES";

    public string TierLabel { get; init; } = "Dados insuficientes";

    /// <summary>0–100 (null quando não há dados para pontuar).</summary>
    public decimal? Score { get; init; }

    public int ExpenseCount { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal? AvgPriceDeviationPercent { get; init; }
    public int PriceAuditCount { get; init; }
    public int CriticalPriceEvents { get; init; }
    public int WarningPriceEvents { get; init; }
    public int OpenComplianceFindings { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
