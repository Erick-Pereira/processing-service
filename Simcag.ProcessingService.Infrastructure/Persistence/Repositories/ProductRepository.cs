using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private const int MaxExternalIdLen = 100;
    private const int MaxNameLen = 500;

    private readonly ProcessingDbContext _db;

    public ProductRepository(ProcessingDbContext db) => _db = db;

    public async Task<Product> UpsertByExternalIdAsync(
        string externalId,
        string source,
        string name,
        decimal price,
        string? category,
        CancellationToken ct = default)
    {
        var ext = Truncate(externalId.Trim(), MaxExternalIdLen);
        var src = source.Trim();
        var displayName = string.IsNullOrWhiteSpace(name) ? ext : Truncate(name.Trim(), MaxNameLen);

        if (price <= 0m)
            throw new ArgumentOutOfRangeException(nameof(price), "Preço deve ser maior que zero.");

        var existing = await _db.Products
            .FirstOrDefaultAsync(p => p.ExternalId == ext && p.Source == src, ct);

        var collected = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.Update(displayName, price, src, category, collected);
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var created = Product.Create(ext, displayName, price, src, category, collected);
        await _db.Products.AddAsync(created, ct);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
