using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Benchmark de mercado persistido após <see cref="Simcag.Shared.Events.PriceAnalyzedEvent"/>.
/// </summary>
public sealed record ProductBenchmarkSnapshot(
    decimal MarketBenchmarkPrice,
    decimal MarketDeviationPercentage,
    string? BenchmarkSource,
    DateTime BenchmarkAt);

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
        string catalogNormalizedName,
        ProductBenchmarkSnapshot? benchmark = null,
        CancellationToken ct = default);

    /// <summary>Benchmarks mais recentes por chave de catálogo (<see cref="Product.CatalogNormalizedName"/>).</summary>
    Task<IReadOnlyDictionary<string, ProductBenchmarkSnapshot>> GetLatestBenchmarksByCatalogKeysAsync(
        IEnumerable<string> catalogNormalizedNames,
        CancellationToken ct = default);

    /// <summary>Produtos com desvio de mercado acima do limiar (insights operacionais).</summary>
    Task<IReadOnlyList<ProductMarketDeviationRow>> ListTopMarketDeviationsAsync(
        decimal minDeviationPercent,
        int take,
        CancellationToken ct = default);
}

public sealed record ProductMarketDeviationRow(
    string ProductName,
    decimal LastPrice,
    decimal MarketBenchmarkPrice,
    decimal MarketDeviationPercentage,
    DateTime BenchmarkAt);
