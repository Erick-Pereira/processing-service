namespace Simcag.ProcessingService.Application.DTOs;

/// <summary>Resposta agregada de insights operacionais (regras determinísticas, tenant-scoped).</summary>
public sealed class OperationalInsightsEnvelope
{
    public DateTime GeneratedAtUtc { get; init; }
    public IReadOnlyList<OperationalInsightDto> Items { get; init; } = Array.Empty<OperationalInsightDto>();
    public string Disclaimer { get; init; } =
        "Estes itens são derivados por regras sobre dados já persistidos (materialized view e séries mensais). " +
        "Não substituem auditoria humana; a confiança reflecte só a solidez da evidência numérica, não causalidade.";

    /// <summary>Versão das regras usada para gerar ou ler este envelope (auditoria / invalidação).</summary>
    public string RuleSetVersion { get; set; } = "";

    /// <summary>Id do snapshot persistido quando servido de cache ou após gravação em tempo real.</summary>
    public Guid? SnapshotId { get; set; }

    /// <summary><c>snapshot</c> = lido de PostgreSQL ainda válido; <c>live</c> = calculado neste pedido.</summary>
    public string? ServedFrom { get; set; }

    /// <summary>Fim da janela de cache do snapshot (UTC), quando aplicável.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Resumo executivo em linguagem operacional (gerado deterministicamente no servidor).</summary>
    public string ExecutiveSummary { get; init; } = "";
}

public sealed class OperationalInsightDto
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Severity { get; init; } = "info";
    public string Confidence { get; init; } = "medium";
    public OperationalInsightPeriodDto? PrimaryPeriod { get; init; }
    public OperationalInsightPeriodDto? ComparePeriod { get; init; }
    public IReadOnlyList<string> DataSources { get; init; } = Array.Empty<string>();
    public string Criteria { get; init; } = "";
    public Dictionary<string, string> Evidence { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Agrupamento na UI: spend | trend | anomaly | alerts | compliance.</summary>
    public string UiGroup { get; init; } = "spend";

    /// <summary>Nível de atenção visual: critical | attention | info.</summary>
    public string Tier { get; init; } = "info";

    /// <summary>0–100: força do sinal para priorização (heurística, não risco financeiro certo).</summary>
    public int ImpactScore { get; init; }

    /// <summary>Frase curta para leitura em &lt; 10 s.</summary>
    public string SimpleExplanation { get; init; } = "";

    /// <summary>Texto operacional com contexto (sem jargão de pipeline).</summary>
    public string DetailedExplanation { get; init; } = "";

    /// <summary>Porque o gestor deve prestar atenção agora.</summary>
    public string WhyItMatters { get; init; } = "";

    /// <summary>Recomendação explícita (tomada de decisão).</summary>
    public string Recommendation { get; init; } = "";

    /// <summary>Próximo passo concreto na aplicação.</summary>
    public string SuggestedAction { get; init; } = "";

    /// <summary>Descrição humana da origem (substitui lista técnica na vista resumida).</summary>
    public string DataOriginLabel { get; init; } = "";

    public string BenchmarkNote { get; init; } = "";
    public string ComplianceNote { get; init; } = "";
    public string AnomalyNote { get; init; } = "";

    public IReadOnlyList<OperationalInsightLinkDto> OperationalLinks { get; init; } = Array.Empty<OperationalInsightLinkDto>();
}

public sealed class OperationalInsightPeriodDto
{
    public DateTime FromInclusive { get; init; }
    public DateTime ToInclusive { get; init; }
}
