namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Ciclo de vida técnico da pipeline (ingestão, enriquecimento, benchmark, persistência).</summary>
public enum ExpenseProcessingStatus
{
    /// <summary>Documento aceite; ainda não persistido ou aguardando primeira etapa.</summary>
    Received = 1,

    /// <summary>Enriquecimento IA / derivação em curso (outros serviços ou passos futuros).</summary>
    Enriching = 2,

    /// <summary>Análise de preço / benchmark web em curso ou aguardada.</summary>
    Benchmarking = 3,

    /// <summary>Persistência da despesa e itens em curso.</summary>
    Persisting = 4,

    /// <summary>Pipeline automática concluída com sucesso.</summary>
    Completed = 5,

    /// <summary>Falha terminal da pipeline (requer intervenção ou retry).</summary>
    Failed = 6,

    /// <summary>Pipeline concluída com degradação controlada (ex.: catálogo sem benchmark).</summary>
    PartiallyCompleted = 7,
}
