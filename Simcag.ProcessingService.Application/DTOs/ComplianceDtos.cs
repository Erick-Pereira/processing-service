using System;
using System.Collections.Generic;

namespace Simcag.ProcessingService.Application.DTOs;

public sealed class ComplianceRuleDefinitionDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultSeverity { get; set; } = "MEDIUM";
    public string Category { get; set; } = string.Empty;
}

public sealed class ExpenseComplianceCommentDto
{
    public Guid Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? AuthorUserId { get; set; }
    public string? AuthorUserName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ExpenseComplianceFindingDto
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public decimal? Confidence { get; set; }
    public string? DetailJson { get; set; }
    public string? EvidenceDocumentIdsJson { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? WaivedAtUtc { get; set; }
    public Guid? WaivedByUserId { get; set; }
    public string? WaivedByUserName { get; set; }
    public string? WaivedReason { get; set; }
    public IReadOnlyList<ExpenseComplianceCommentDto> Comments { get; set; } = Array.Empty<ExpenseComplianceCommentDto>();
}

public sealed class ExpenseComplianceSnapshotDto
{
    public Guid ExpenseId { get; set; }
    public int ComplianceScore { get; set; }
    public int OutstandingCount { get; set; }
    public int ClearCount { get; set; }
    public int WaivedCount { get; set; }
    public int HighRiskOpenCount { get; set; }
    public IReadOnlyList<ExpenseComplianceFindingDto> Findings { get; set; } = Array.Empty<ExpenseComplianceFindingDto>();
}

public sealed class ComplianceDashboardDto
{
    public int ComplianceScore { get; set; }
    public int OutstandingFindings { get; set; }
    public int ClearFindings { get; set; }
    public int WaivedFindings { get; set; }
    public int HighRiskOpen { get; set; }
    public int DistinctExpensesWithOpen { get; set; }
}
