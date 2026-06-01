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
            .AsSplitQuery()
            .Include(e => e.Items)
            .Include(e => e.Payments)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Expense?> GetByRawDocumentIdAsync(Guid rawDocumentId, CancellationToken ct = default) =>
        _db.Expenses.FirstOrDefaultAsync(e => e.RawDocumentId == rawDocumentId, ct);

    public async Task<(IReadOnlyList<Expense> Items, int Total)> ListAsync(
        ExpenseStatus? legacyStatus,
        ExpenseProcessingStatus? processingStatus,
        ExpenseApprovalStatus? approvalStatus,
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

        if (legacyStatus.HasValue)
            filtered = ApplyLegacyStatusFilter(filtered, legacyStatus.Value);
        if (processingStatus.HasValue)
            filtered = filtered.Where(e => e.ProcessingStatus == processingStatus.Value);
        if (approvalStatus.HasValue)
            filtered = filtered.Where(e => e.ApprovalStatus == approvalStatus.Value);
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

    /// <summary>
    /// Filtro legado por <see cref="ExpenseStatus"/>: <c>Pending</c> corresponde à fila de aprovação humana
    /// (processamento já terminou com sucesso ou degradação controlada), excluindo falhas técnicas e liquidação total.
    /// </summary>
    private static IQueryable<Expense> ApplyLegacyStatusFilter(IQueryable<Expense> query, ExpenseStatus status) =>
        status switch
        {
            ExpenseStatus.Pending => query.Where(e =>
                e.ApprovalStatus == ExpenseApprovalStatus.PendingApproval
                && e.SettlementStatus != ExpenseSettlementStatus.Paid
                && (e.ProcessingStatus == ExpenseProcessingStatus.Completed
                    || e.ProcessingStatus == ExpenseProcessingStatus.PartiallyCompleted)),
            ExpenseStatus.Approved => query.Where(e =>
                e.ApprovalStatus == ExpenseApprovalStatus.Approved
                && e.SettlementStatus != ExpenseSettlementStatus.Paid),
            ExpenseStatus.Paid => query.Where(e => e.SettlementStatus == ExpenseSettlementStatus.Paid),
            ExpenseStatus.Cancelled => query.Where(e => e.ApprovalStatus == ExpenseApprovalStatus.Cancelled),
            ExpenseStatus.Rejected => query.Where(e => e.ApprovalStatus == ExpenseApprovalStatus.Rejected),
            ExpenseStatus.ProcessingFailed => query.Where(e => e.ProcessingStatus == ExpenseProcessingStatus.Failed),
            _ => query,
        };

    public Task<int> ReassignSupplierAsync(Guid fromSupplierId, Guid toSupplierId, decimal? newConfidenceScore, CancellationToken ct = default) =>
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
