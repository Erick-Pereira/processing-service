using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Simcag.ProcessingService.Tests.UseCases;

public sealed class ProductRepositoryBenchmarkTests
{
    [Fact]
    public async Task Upsert_persists_market_benchmark_when_provided()
    {
        await using var db = CreateDb();
        var repo = new ProductRepository(db);

        await repo.UpsertByExternalIdAsync(
            "doc:1",
            "price-analysis",
            "Câmera IP 2MP",
            890m,
            "Segurança",
            "camera-ip-2mp",
            new ProductBenchmarkSnapshot(185m, 381.08m, "curated", DateTime.UtcNow));

        var benchmarks = await repo.GetLatestBenchmarksByCatalogKeysAsync(["camera-ip-2mp"]);
        Assert.True(benchmarks.TryGetValue("camera-ip-2mp", out var snap));
        Assert.Equal(185m, snap.MarketBenchmarkPrice);
        Assert.Equal(381.08m, snap.MarketDeviationPercentage);
    }

    [Fact]
    public async Task Upsert_without_benchmark_does_not_clear_existing_benchmark()
    {
        await using var db = CreateDb();
        var repo = new ProductRepository(db);

        await repo.UpsertByExternalIdAsync(
            "doc:1",
            "price-analysis",
            "Câmera IP 2MP",
            890m,
            "Segurança",
            "camera-ip-2mp",
            new ProductBenchmarkSnapshot(185m, 381.08m, "curated", DateTime.UtcNow));

        await repo.UpsertByExternalIdAsync(
            "doc:2",
            "price-analysis",
            "Câmera IP 2MP",
            900m,
            "Segurança",
            "camera-ip-2mp");

        var row = await db.Products.SingleAsync();
        Assert.Equal(185m, row.MarketBenchmarkPrice);
        Assert.Equal(900m, row.Price);
    }

    private static ProcessingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ProcessingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProcessingDbContext(options);
    }
}
