using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ProcessingDbContext _db;

    public ExpenseRepository(ProcessingDbContext db) => _db = db;

    public Task<Expense?> GetByIdAsync(Guid id, Guid condominioId, CancellationToken ct = default) =>
        _db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.CondominioId == condominioId, ct);

    public Task<Expense?> GetByRawDocumentIdAsync(Guid rawDocumentId, CancellationToken ct = default) =>
        _db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.RawDocumentId == rawDocumentId, ct);

    public async Task<IReadOnlyList<Expense>> ListAsync(
        Guid condominioId,
        DateTime? from,
        DateTime? to,
        string? category,
        Guid? supplierId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var q = _db.Expenses.AsNoTracking().Where(e => e.CondominioId == condominioId);
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
        if (to.HasValue) q = q.Where(e => e.Date <= to.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        if (supplierId.HasValue) q = q.Where(e => e.SupplierId == supplierId);
        return await q.OrderByDescending(e => e.Date).Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Guid condominioId,
        DateTime? from,
        DateTime? to,
        string? category,
        Guid? supplierId,
        CancellationToken ct = default)
    {
        var q = _db.Expenses.AsNoTracking().Where(e => e.CondominioId == condominioId);
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
        if (to.HasValue) q = q.Where(e => e.Date <= to.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        if (supplierId.HasValue) q = q.Where(e => e.SupplierId == supplierId);
        return q.CountAsync(ct);
    }

    public async Task<decimal> SumAmountAsync(
        Guid condominioId,
        DateTime? from,
        DateTime? to,
        string? category,
        CancellationToken ct = default)
    {
        var q = _db.Expenses.AsNoTracking().Where(e => e.CondominioId == condominioId);
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
        if (to.HasValue) q = q.Where(e => e.Date <= to.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        return await q.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
    }

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
    {
        await _db.Expenses.AddAsync(expense, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        _db.Expenses.Update(expense);
        await _db.SaveChangesAsync(ct);
    }
}
