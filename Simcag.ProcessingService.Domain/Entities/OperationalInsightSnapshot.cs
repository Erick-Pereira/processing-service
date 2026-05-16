using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Snapshot imutável de insights operacionais por tenant (evita recomputação e permite auditoria temporal).
/// </summary>
public sealed class OperationalInsightSnapshot : IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Versão das regras / do payload; mudanças de limiar devem bump para invalidar leituras antigas.</summary>
    public string RuleSetVersion { get; private set; } = "";

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Envelope serializado (JSON do DTO de insights operacionais).</summary>
    [NotAudited]
    public string PayloadJson { get; private set; } = "{}";

    /// <summary>Contexto opcional do condomínio (nome, regime, etc.) quando integrado — hoje pode ser null.</summary>
    [NotAudited]
    public string? ContextJson { get; private set; }

    private OperationalInsightSnapshot() { }

    public static OperationalInsightSnapshot Create(
        Guid tenantId,
        string ruleSetVersion,
        TimeSpan ttl,
        string payloadJson,
        string? contextJson)
    {
        var now = DateTime.UtcNow;
        return new OperationalInsightSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleSetVersion = ruleSetVersion,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(ttl),
            PayloadJson = payloadJson,
            ContextJson = contextJson
        };
    }
}
