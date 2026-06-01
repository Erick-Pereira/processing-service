using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ProcessingDbContext _db;
    private readonly ITenantContext _tenant;

    public SupplierRepository(ProcessingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Suppliers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == _tenant.TenantId, ct);

    public Task<Supplier?> GetByDocumentAsync(string document, CancellationToken ct = default)
    {
        var digits = new string([.. (document ?? string.Empty).Where(char.IsDigit)]);
        if (digits.Length != 11 && digits.Length != 14) return Task.FromResult<Supplier?>(null);
        return _db.Suppliers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Document == digits && s.TenantId == _tenant.TenantId, ct);
    }

    public Task<Supplier?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) =>
        _db.Suppliers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.NormalizedName == normalizedName && s.TenantId == _tenant.TenantId, ct);

    public async Task<IReadOnlyList<Supplier>> ListAsync(string? category, CancellationToken ct = default)
    {
        var q = _db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(s => s.Category == category);
        return await q.OrderBy(s => s.NormalizedName).ToListAsync(ct);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default)
    {
        await _db.Suppliers.AddAsync(supplier, ct);
    }

    public async Task<Supplier> UpsertByDocumentOrNameAsync(
        string name,
        string document,
        string? category,
        CancellationToken ct = default)
    {
        var existing = await GetByDocumentAsync(document, ct);
        if (existing is null)
        {
            existing = await GetByNormalizedNameAsync(Supplier.NormalizeName(name), ct);
        }

        if (existing is not null)
        {
            existing.Update(name, document, contact: null, category, null);
            return existing;
        }

        var fresh = Supplier.Create(_tenant.TenantId, name, document, contact: null, category);
        await _db.Suppliers.AddAsync(fresh, ct);
        return fresh;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
