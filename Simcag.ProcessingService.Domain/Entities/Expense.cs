using System;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Despesa condominial estruturada — agregado raiz canônico v1 do pipeline.
/// Substitui conceitualmente <see cref="Product"/> (legado, vocabulário "produto").
/// Uma <c>Expense</c> é gerada por documento ingerido e enriquecido pelo AI.
/// </summary>
public sealed class Expense
{
    public Guid Id { get; private set; }

    /// <summary>Tenant (CondominioId).</summary>
    public Guid CondominioId { get; private set; }

    /// <summary>Documento de origem (idempotência).</summary>
    public Guid RawDocumentId { get; private set; }

    /// <summary>Fornecedor (FK lógica para <see cref="Supplier"/>).</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Categoria normalizada (ex.: "Limpeza Predial").</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>Valor pago.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Moeda (default BRL).</summary>
    public string Currency { get; private set; } = "BRL";

    /// <summary>Data da despesa (UTC).</summary>
    public DateTime Date { get; private set; }

    /// <summary>Região (UF).</summary>
    public string Region { get; private set; } = string.Empty;

    /// <summary>Confiança média do enriquecimento IA (0.0-1.0).</summary>
    public decimal ConfidenceScore { get; private set; }

    /// <summary>True quando <see cref="ConfidenceScore"/> abaixo do limite (default 0.6).</summary>
    public bool LowConfidence { get; private set; }

    /// <summary>Texto bruto original (referência para auditoria).</summary>
    public string? RawText { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Expense() { }

    public static Expense Create(
        Guid condominioId,
        Guid rawDocumentId,
        Guid? supplierId,
        string category,
        decimal amount,
        DateTime date,
        string region,
        decimal confidenceScore,
        decimal confidenceThreshold = 0.6m,
        string? rawText = null)
    {
        if (condominioId == Guid.Empty) throw new ArgumentException("CondominioId obrigatório.");
        if (rawDocumentId == Guid.Empty) throw new ArgumentException("RawDocumentId obrigatório.");
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category obrigatória.");
        if (amount <= 0) throw new ArgumentException("Amount deve ser > 0.");

        var now = DateTime.UtcNow;
        return new Expense
        {
            Id = Guid.NewGuid(),
            CondominioId = condominioId,
            RawDocumentId = rawDocumentId,
            SupplierId = supplierId,
            Category = category.Trim(),
            Amount = amount,
            Date = date,
            Region = string.IsNullOrWhiteSpace(region) ? "BR" : region.ToUpperInvariant(),
            ConfidenceScore = confidenceScore,
            LowConfidence = confidenceScore < confidenceThreshold,
            RawText = rawText,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AssignSupplier(Guid supplierId)
    {
        if (supplierId == Guid.Empty) throw new ArgumentException("SupplierId inválido.");
        SupplierId = supplierId;
        UpdatedAt = DateTime.UtcNow;
    }
}
