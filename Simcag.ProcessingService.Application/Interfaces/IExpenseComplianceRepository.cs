using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IExpenseComplianceRepository
{
    Task<IReadOnlyList<ExpenseComplianceFinding>> ListByExpenseAsync(Guid expenseId, CancellationToken ct = default);

    /// <summary>Entidades rastreadas para merge na reavaliação.</summary>
    Task<List<ExpenseComplianceFinding>> ListTrackedForExpenseAsync(Guid expenseId, CancellationToken ct = default);

    Task<IReadOnlyList<ExpenseComplianceComment>> ListCommentsByFindingAsync(Guid findingId, CancellationToken ct = default);

    Task<ExpenseComplianceFinding?> GetFindingAsync(Guid expenseId, Guid findingId, CancellationToken ct = default);

    Task<(IReadOnlyList<ExpenseComplianceFinding> Items, int Total)> ListTenantFindingsAsync(
        string? status,
        string? severity,
        Guid? expenseId,
        int skip,
        int take,
        CancellationToken ct = default);

    Task<(int Outstanding, int Clear, int Waived, int HighOpen)> CountTenantAsync(CancellationToken ct = default);

    Task<int> CountDistinctExpensesWithOpenFindingsAsync(CancellationToken ct = default);

    void Add(ExpenseComplianceFinding finding);

    void AddComment(ExpenseComplianceComment comment);

    Task SaveChangesAsync(CancellationToken ct = default);
}
