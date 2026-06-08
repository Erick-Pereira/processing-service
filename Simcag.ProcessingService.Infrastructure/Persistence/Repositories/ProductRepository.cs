using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private const int MaxExternalIdLen = 100;
    private const int MaxNameLen = 500;
    private const int MaxBenchmarkSourceLen = 200;

    private readonly ProcessingDbContext _db;

    public ProductRepository(ProcessingDbContext db) => _db = db;

    public async Task<Product> UpsertByExternalIdAsync(
        string externalId,
        string source,
        string name,
        decimal price,
        string? category,
        string catalogNormalizedName,
        ProductBenchmarkSnapshot? benchmark = null,
        CancellationToken ct = default)
    {
        var ext = Truncate(externalId.Trim(), MaxExternalIdLen);
        var src = source.Trim();
        var displayName = string.IsNullOrWhiteSpace(name) ? ext : Truncate(name.Trim(), MaxNameLen);
        var catalogKey = Truncate(string.IsNullOrWhiteSpace(catalogNormalizedName) ? displayName : catalogNormalizedName.Trim(), MaxNameLen);

        if (price <= 0m)
            throw new ArgumentOutOfRangeException(nameof(price), "Preço deve ser maior que zero.");

        var collected = DateTime.UtcNow;
        var normalized = Product.ComputeNormalizedName(displayName);

        Product? target = null;

        if (normalized.Length > 0
            && string.Equals(src, "price-analysis", StringComparison.OrdinalIgnoreCase))
        {
            target = await _db.Products
                .FirstOrDefaultAsync(p => p.Source == src && p.NormalizedName == normalized, ct);
        }

        target ??= await _db.Products
            .FirstOrDefaultAsync(p => p.ExternalId == ext && p.Source == src, ct);

        if (target is not null)
        {
            target.Update(displayName, price, src, category, collected, catalogKey);
            ApplyBenchmark(target, benchmark);
            await _db.SaveChangesAsync(ct);
            return target;
        }

        var created = Product.Create(ext, displayName, price, src, category, collected, catalogKey);
        ApplyBenchmark(created, benchmark);
        await _db.Products.AddAsync(created, ct);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<IReadOnlyDictionary<string, ProductBenchmarkSnapshot>> GetLatestBenchmarksByCatalogKeysAsync(
        IEnumerable<string> catalogNormalizedNames,
        CancellationToken ct = default)
    {
        var keys = catalogNormalizedNames
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
            return new Dictionary<string, ProductBenchmarkSnapshot>(StringComparer.OrdinalIgnoreCase);

        var rows = await _db.Products
            .AsNoTracking()
            .Where(p => p.Source == "price-analysis"
                        && keys.Contains(p.CatalogNormalizedName)
                        && p.MarketBenchmarkPrice != null
                        && p.LastBenchmarkAt != null)
            .Select(p => new
            {
                p.CatalogNormalizedName,
                p.MarketBenchmarkPrice,
                p.MarketDeviationPercentage,
                p.BenchmarkSource,
                p.LastBenchmarkAt,
            })
            .ToListAsync(ct);

        var result = new Dictionary<string, ProductBenchmarkSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in rows.GroupBy(r => r.CatalogNormalizedName, StringComparer.OrdinalIgnoreCase))
        {
            var latest = group
                .OrderByDescending(r => r.LastBenchmarkAt)
                .First();
            if (latest.MarketBenchmarkPrice is not > 0m || latest.LastBenchmarkAt is null)
                continue;

            result[group.Key] = new ProductBenchmarkSnapshot(
                latest.MarketBenchmarkPrice.Value,
                latest.MarketDeviationPercentage ?? 0m,
                latest.BenchmarkSource,
                latest.LastBenchmarkAt.Value);
        }

        return result;
    }

    public async Task<IReadOnlyList<ProductMarketDeviationRow>> ListTopMarketDeviationsAsync(
        decimal minDeviationPercent,
        int take,
        CancellationToken ct = default)
    {
        if (take <= 0)
            return Array.Empty<ProductMarketDeviationRow>();

        var threshold = Math.Max(0m, minDeviationPercent);
        var rows = await _db.Products
            .AsNoTracking()
            .Where(p => p.Source == "price-analysis"
                        && p.MarketBenchmarkPrice != null
                        && p.MarketBenchmarkPrice > 0
                        && p.MarketDeviationPercentage != null
                        && p.MarketDeviationPercentage >= threshold)
            .OrderByDescending(p => p.MarketDeviationPercentage)
            .ThenByDescending(p => p.LastBenchmarkAt)
            .Take(take)
            .Select(p => new ProductMarketDeviationRow(
                p.Name,
                p.Price,
                p.MarketBenchmarkPrice!.Value,
                p.MarketDeviationPercentage!.Value,
                p.LastBenchmarkAt ?? p.CollectionDate))
            .ToListAsync(ct);

        return rows;
    }

    private static void ApplyBenchmark(Product product, ProductBenchmarkSnapshot? benchmark)
    {
        if (benchmark is null || benchmark.MarketBenchmarkPrice <= 0m)
            return;

        product.UpdateMarketBenchmark(
            benchmark.MarketBenchmarkPrice,
            benchmark.MarketDeviationPercentage,
            Truncate(benchmark.BenchmarkSource ?? string.Empty, MaxBenchmarkSourceLen),
            benchmark.BenchmarkAt);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
