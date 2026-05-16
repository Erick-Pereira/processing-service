using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class ExpenseComplianceRepository : IExpenseComplianceRepository
{
    private readonly ProcessingDbContext _db;

    public ExpenseComplianceRepository(ProcessingDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExpenseComplianceFinding>> ListByExpenseAsync(Guid expenseId, CancellationToken ct = default) =>
        await _db.Set<ExpenseComplianceFinding>()
            .AsNoTracking()
            .Where(x => x.ExpenseId == expenseId)
            .OrderBy(x => x.RuleCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public Task<List<ExpenseComplianceFinding>> ListTrackedForExpenseAsync(Guid expenseId, CancellationToken ct = default) =>
        _db.Set<ExpenseComplianceFinding>()
            .Where(x => x.ExpenseId == expenseId)
            .OrderBy(x => x.RuleCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExpenseComplianceComment>> ListCommentsByFindingAsync(Guid findingId, CancellationToken ct = default) =>
        await _db.Set<ExpenseComplianceComment>()
            .AsNoTracking()
            .Where(x => x.FindingId == findingId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public Task<ExpenseComplianceFinding?> GetFindingAsync(Guid expenseId, Guid findingId, CancellationToken ct = default) =>
        _db.Set<ExpenseComplianceFinding>()
            .FirstOrDefaultAsync(x => x.ExpenseId == expenseId && x.Id == findingId, ct);

    public async Task<(IReadOnlyList<ExpenseComplianceFinding> Items, int Total)> ListTenantFindingsAsync(
        string? status,
        string? severity,
        Guid? expenseId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var q = _db.Set<ExpenseComplianceFinding>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            q = q.Where(x => x.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var sev = severity.Trim().ToUpperInvariant();
            q = q.Where(x => x.Severity == sev);
        }

        if (expenseId.HasValue)
            q = q.Where(x => x.ExpenseId == expenseId.Value);

        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var items = await q
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<(int Outstanding, int Clear, int Waived, int HighOpen)> CountTenantAsync(CancellationToken ct = default)
    {
        var baseQ = _db.Set<ExpenseComplianceFinding>().AsNoTracking();
        var outstanding = await baseQ.CountAsync(x => x.Status == "OUTSTANDING", ct).ConfigureAwait(false);
        var clear = await baseQ.CountAsync(x => x.Status == "CLEAR", ct).ConfigureAwait(false);
        var waived = await baseQ.CountAsync(x => x.Status == "WAIVED", ct).ConfigureAwait(false);
        var highOpen = await baseQ.CountAsync(
            x => x.Status == "OUTSTANDING" && (x.Severity == "HIGH" || x.Severity == "CRITICAL"),
            ct).ConfigureAwait(false);
        return (outstanding, clear, waived, highOpen);
    }

    public Task<int> CountDistinctExpensesWithOpenFindingsAsync(CancellationToken ct = default) =>
        _db.Set<ExpenseComplianceFinding>()
            .AsNoTracking()
            .Where(x => x.Status == "OUTSTANDING")
            .Select(x => x.ExpenseId)
            .Distinct()
            .CountAsync(ct);

    public void Add(ExpenseComplianceFinding finding) => _db.Set<ExpenseComplianceFinding>().Add(finding);

    public void AddComment(ExpenseComplianceComment comment) => _db.Set<ExpenseComplianceComment>().Add(comment);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
