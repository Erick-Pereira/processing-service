using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ProcessingDbContext _db;

    public SupplierRepository(ProcessingDbContext db) => _db = db;

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Supplier?> GetByCnpjAsync(string cnpj, CancellationToken ct = default)
    {
        var digits = new string([.. (cnpj ?? string.Empty).Where(char.IsDigit)]);
        if (digits.Length != 14) return Task.FromResult<Supplier?>(null);
        return _db.Suppliers.FirstOrDefaultAsync(s => s.Cnpj == digits, ct);
    }

    public Task<Supplier?> GetByNormalizedNameAsync(Guid? condominioId, string normalizedName, CancellationToken ct = default) =>
        _db.Suppliers.FirstOrDefaultAsync(
            s => s.CondominioId == condominioId && s.NormalizedName == normalizedName, ct);

    public async Task<IReadOnlyList<Supplier>> ListAsync(Guid condominioId, string? category, CancellationToken ct = default)
    {
        var q = _db.Suppliers.AsNoTracking()
            .Where(s => s.CondominioId == condominioId && s.IsActive);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(s => s.Category == category);
        return await q.OrderBy(s => s.NormalizedName).ToListAsync(ct);
    }

    public async Task<Supplier> UpsertByCnpjOrNameAsync(
        Guid condominioId,
        string rawName,
        string? cnpj,
        string? category,
        CancellationToken ct = default)
    {
        Supplier? existing = null;
        if (!string.IsNullOrWhiteSpace(cnpj))
            existing = await GetByCnpjAsync(cnpj!, ct);

        if (existing is null)
        {
            var normalized = NormalizeForLookup(rawName);
            existing = await GetByNormalizedNameAsync(condominioId, normalized, ct);
        }

        if (existing is not null)
        {
            existing.Update(cnpj, rawName, category);
            _db.Suppliers.Update(existing);
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var fresh = Supplier.Create(condominioId, rawName, cnpj, category);
        await _db.Suppliers.AddAsync(fresh, ct);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }

    private static string NormalizeForLookup(string raw)
    {
        var s = raw.Trim().ToUpperInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ");
    }
}
