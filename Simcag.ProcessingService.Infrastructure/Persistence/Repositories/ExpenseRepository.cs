using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ProcessingDbContext _db;

    public ExpenseRepository(ProcessingDbContext db) => _db = db;

    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Expense?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default) =>
        _db.Expenses
            .Include(e => e.Items)
            .Include(e => e.Payments)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Expense?> GetByRawDocumentIdAsync(Guid rawDocumentId, CancellationToken ct = default) =>
        _db.Expenses.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.RawDocumentId == rawDocumentId, ct);

    public async Task<(IReadOnlyList<Expense> Items, int Total)> ListAsync(
        ExpenseStatus? status,
        string? category,
        Guid? supplierId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        bool includePayments = false,
        CancellationToken ct = default)
    {
        var filtered = _db.Expenses.AsNoTracking();

        if (status.HasValue) filtered = filtered.Where(e => e.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(category)) filtered = filtered.Where(e => e.Category == category);
        if (supplierId.HasValue) filtered = filtered.Where(e => e.SupplierId == supplierId);
        if (from.HasValue) filtered = filtered.Where(e => e.IssueDate >= from.Value);
        if (to.HasValue) filtered = filtered.Where(e => e.IssueDate <= to.Value);

        var total = await filtered.CountAsync(ct);

        var page = includePayments
            ? filtered.Include(e => e.Payments)
            : filtered;

        var items = await page.OrderByDescending(e => e.IssueDate)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<int> ReassignSupplierAsync(Guid fromSupplierId, Guid toSupplierId, CancellationToken ct = default) =>
        _db.Expenses
            .Where(e => e.SupplierId == fromSupplierId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.SupplierId, toSupplierId)
                    .SetProperty(e => e.UpdatedAt, DateTime.UtcNow),
                ct);

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
    {
        await _db.Expenses.AddAsync(expense, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
