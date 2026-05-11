using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Catálogo de produtos inferidos pelo pipeline (ex.: linhas de despesa + análise de preço).
/// Chave natural: <see cref="Product.ExternalId"/> + <see cref="Product.Source"/>.
/// </summary>
public interface IProductRepository
{
    /// <summary>Insere ou atualiza pelo par (externalId, source).</summary>
    Task<Product> UpsertByExternalIdAsync(
        string externalId,
        string source,
        string name,
        decimal price,
        string? category,
        CancellationToken ct = default);
}
