using System;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Achado de conformidade operacional por despesa (regra + estado + exceção auditável).
/// </summary>
public sealed class ExpenseComplianceFinding : IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ExpenseId { get; private set; }

    /// <summary>Código estável da regra (ex.: LOW_CONFIDENCE).</summary>
    public string RuleCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>LOW | MEDIUM | HIGH | CRITICAL</summary>
    public string Severity { get; private set; } = "MEDIUM";

    /// <summary>OUTSTANDING | CLEAR | WAIVED</summary>
    public string Status { get; private set; } = "OUTSTANDING";

    /// <summary>RULE | AI | MANUAL</summary>
    public string Origin { get; private set; } = "RULE";

    public decimal? Confidence { get; private set; }
    public string? DetailJson { get; private set; }
    public string? EvidenceDocumentIdsJson { get; private set; }
    public DateTime EvaluatedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? WaivedAtUtc { get; private set; }
    public Guid? WaivedByUserId { get; private set; }
    public string? WaivedByUserName { get; private set; }
    public string? WaivedReason { get; private set; }

    private ExpenseComplianceFinding() { }

    public static ExpenseComplianceFinding Create(
        Guid tenantId,
        Guid expenseId,
        string ruleCode,
        string title,
        string description,
        string severity,
        string status,
        string origin,
        decimal? confidence,
        string? detailJson,
        DateTime evaluatedAtUtc)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId obrigatório.");
        if (expenseId == Guid.Empty) throw new DomainException("ExpenseId obrigatório.");
        if (string.IsNullOrWhiteSpace(ruleCode)) throw new DomainException("RuleCode obrigatório.");

        var now = DateTime.UtcNow;
        return new ExpenseComplianceFinding
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExpenseId = expenseId,
            RuleCode = ruleCode.Trim().ToUpperInvariant(),
            Title = (title ?? string.Empty).Trim(),
            Description = (description ?? string.Empty).Trim(),
            Severity = NormalizeSeverity(severity),
            Status = NormalizeEngineStatus(status),
            Origin = NormalizeOrigin(origin),
            Confidence = confidence,
            DetailJson = detailJson,
            EvaluatedAtUtc = evaluatedAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    /// <summary>Atualiza resultado do motor mantendo exceção registada (waived).</summary>
    public void ApplyEngineUpdate(
        string title,
        string description,
        string severity,
        string status,
        decimal? confidence,
        string? detailJson,
        DateTime evaluatedAtUtc)
    {
        if (IsWaived)
        {
            Title = string.IsNullOrWhiteSpace(title) ? Title : title.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? Description : description.Trim();
            EvaluatedAtUtc = evaluatedAtUtc;
            UpdatedAtUtc = DateTime.UtcNow;
            return;
        }

        Title = (title ?? string.Empty).Trim();
        Description = (description ?? string.Empty).Trim();
        Severity = NormalizeSeverity(severity);
        Status = NormalizeEngineStatus(status);
        Confidence = confidence;
        DetailJson = detailJson;
        EvaluatedAtUtc = evaluatedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Waive(Guid? userId, string? userName, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Justificativa de override é obrigatória.");
        if (IsWaived) return;

        WaivedAtUtc = DateTime.UtcNow;
        WaivedByUserId = userId;
        WaivedByUserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
        WaivedReason = reason.Trim();
        Status = "WAIVED";
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetEvidenceDocumentIdsJson(string? json)
    {
        EvidenceDocumentIdsJson = string.IsNullOrWhiteSpace(json) ? null : json.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsWaived => WaivedAtUtc.HasValue;

    private static string NormalizeSeverity(string s)
    {
        var u = (s ?? "MEDIUM").Trim().ToUpperInvariant();
        return u is "LOW" or "MEDIUM" or "HIGH" or "CRITICAL" ? u : "MEDIUM";
    }

    private static string NormalizeEngineStatus(string s)
    {
        var u = (s ?? "OUTSTANDING").Trim().ToUpperInvariant();
        return u is "OUTSTANDING" or "CLEAR" or "WAIVED" ? u : "OUTSTANDING";
    }

    private static string NormalizeOrigin(string s)
    {
        var u = (s ?? "RULE").Trim().ToUpperInvariant();
        return u is "RULE" or "AI" or "MANUAL" ? u : "RULE";
    }
}
