using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ProcessingDbContext _db;

    public PaymentRepository(ProcessingDbContext db) => _db = db;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Payment>> ListByExpenseAsync(Guid expenseId, CancellationToken ct = default) =>
        await _db.Payments.AsNoTracking()
            .Where(p => p.ExpenseId == expenseId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Payment> Items, int Total)> ListAsync(
        Guid? expenseId,
        DateTime? from,
        DateTime? to,
        bool? refunded,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var q = _db.Payments.AsNoTracking();
        if (expenseId.HasValue) q = q.Where(p => p.ExpenseId == expenseId);
        if (from.HasValue) q = q.Where(p => p.PaymentDate >= from.Value);
        if (to.HasValue) q = q.Where(p => p.PaymentDate <= to.Value);
        if (refunded.HasValue) q = q.Where(p => p.IsRefunded == refunded.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.PaymentDate)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _db.Payments.AddAsync(payment, ct);
    }
}
