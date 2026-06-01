using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Repositório de write-side para o agregado <see cref="Expense"/>.
/// Reads de dashboard são responsabilidade do <c>IDashboardQueryRepository</c> (Dapper).
/// </summary>
public interface IExpenseRepository
{
    /// <summary>Carrega Expense incluindo Items e Payments — usado pelos handlers que vão mutar o agregado.</summary>
    Task<Expense?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default);

    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Expense?> GetByRawDocumentIdAsync(Guid rawDocumentId, CancellationToken ct = default);

    Task<(IReadOnlyList<Expense> Items, int Total)> ListAsync(
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
        CancellationToken ct = default);

    /// <summary>Atualiza SupplierId em todas as despesas do tenant atual que apontam para <paramref name="fromSupplierId"/>, e atualiza o score de confiança do fornecedor associado.</summary>
    Task<int> ReassignSupplierAsync(Guid fromSupplierId, Guid toSupplierId, decimal? newConfidenceScore, CancellationToken ct = default);

    Task AddAsync(Expense expense, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
