using System.Collections.Generic;
using Simcag.ProcessingService.Application.DTOs;

namespace Simcag.ProcessingService.Application.Compliance;

/// <summary>Catálogo humano das regras operacionais (espelha o motor em <see cref="ExpenseComplianceEvaluator"/>).</summary>
public static class ComplianceRuleCatalog
{
    public static IReadOnlyList<ComplianceRuleDefinitionDto> All { get; } = new List<ComplianceRuleDefinitionDto>
    {
        new()
        {
            Code = "LOW_CONFIDENCE",
            Title = "Confiança do enriquecimento",
            Description = "Verifica score e flag de baixa confiança da despesa.",
            DefaultSeverity = "HIGH",
            Category = "IA",
        },
        new()
        {
            Code = "PIPELINE_FAILED",
            Title = "Falha de pipeline",
            Description = "Processamento técnico em estado Failed.",
            DefaultSeverity = "CRITICAL",
            Category = "Pipeline",
        },
        new()
        {
            Code = "PIPELINE_INCOMPLETE",
            Title = "Pipeline incompleta",
            Description = "Processamento ainda não concluído.",
            DefaultSeverity = "MEDIUM",
            Category = "Pipeline",
        },
        new()
        {
            Code = "PIPELINE_HEALTH",
            Title = "Saúde da pipeline",
            Description = "Processamento concluído (referência positiva).",
            DefaultSeverity = "LOW",
            Category = "Pipeline",
        },
        new()
        {
            Code = "APPROVAL_SLA",
            Title = "SLA de aprovação",
            Description = "Aprovação humana pendente além de 14 dias desde a emissão.",
            DefaultSeverity = "MEDIUM",
            Category = "Governança",
        },
        new()
        {
            Code = "BENCHMARK_DEVIATION",
            Title = "Benchmark de mercado",
            Description = "Desvio de preço inferido a partir de auditoria PriceAnalyzed.",
            DefaultSeverity = "HIGH",
            Category = "Mercado",
        },
        new()
        {
            Code = "SOURCE_DOCUMENT",
            Title = "Documento de origem",
            Description = "Rastreabilidade RawDocumentId vs ingestão.",
            DefaultSeverity = "MEDIUM",
            Category = "Evidência",
        },
    };
}
