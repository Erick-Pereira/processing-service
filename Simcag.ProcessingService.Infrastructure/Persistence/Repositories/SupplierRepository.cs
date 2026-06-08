using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.UseCases.Suppliers;
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

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, string>();

        var rows = await _db.Suppliers.AsNoTracking()
            .Where(s => idList.Contains(s.Id) && s.TenantId == _tenant.TenantId)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

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
        var trimmedName = string.IsNullOrWhiteSpace(name) ? "Fornecedor não identificado" : name.Trim();
        var normalized = Supplier.NormalizeName(trimmedName);
        var documentDigits = new string([.. (document ?? string.Empty).Where(char.IsDigit)]);

        // 1) Match por razão social — não altera cadastro existente.
        var byName = await GetByNormalizedNameAsync(normalized, ct);
        if (byName is not null)
            return byName;

        // 2) Match por documento fiscal.
        if (documentDigits.Length is 11 or 14)
        {
            var byDoc = await GetByDocumentAsync(documentDigits, ct);
            if (byDoc is not null)
            {
                if (SupplierUpsertPolicy.ShouldUpdateExisting(byDoc, trimmedName))
                {
                    byDoc.Update(trimmedName, documentDigits, contact: null, category, null);
                    return byDoc;
                }

                // Mesmo CNPJ, razão social diferente — cadastro distinto (homologação / colisão).
                var synthetic = SupplierUpsertPolicy.BuildSyntheticDocumentForName(normalized);
                var bySynthetic = await GetByDocumentAsync(synthetic, ct);
                if (bySynthetic is not null)
                    return bySynthetic;

                var alt = Supplier.Create(_tenant.TenantId, trimmedName, synthetic, contact: null, category);
                await _db.Suppliers.AddAsync(alt, ct);
                return alt;
            }
        }

        var docForCreate = documentDigits.Length is 11 or 14 ? documentDigits : "00000000000000";
        var fresh = Supplier.Create(_tenant.TenantId, trimmedName, docForCreate, contact: null, category);
        await _db.Suppliers.AddAsync(fresh, ct);
        return fresh;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
