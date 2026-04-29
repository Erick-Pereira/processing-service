using System;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Repositório canônico v1 para a entidade <see cref="Expense"/>.
/// </summary>
public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(Guid id, Guid condominioId, CancellationToken ct = default);

    Task<Expense?> GetByRawDocumentIdAsync(Guid rawDocumentId, CancellationToken ct = default);

    Task<IReadOnlyList<Expense>> ListAsync(
        Guid condominioId,
        DateTime? from,
        DateTime? to,
        string? category,
        Guid? supplierId,
        int skip,
        int take,
        CancellationToken ct = default);

    Task<int> CountAsync(
        Guid condominioId,
        DateTime? from,
        DateTime? to,
        string? category,
        Guid? supplierId,
        CancellationToken ct = default);

    Task<decimal> SumAmountAsync(
        Guid condominioId,
        DateTime? from,
        DateTime? to,
        string? category,
        CancellationToken ct = default);

    Task AddAsync(Expense expense, CancellationToken ct = default);

    Task UpdateAsync(Expense expense, CancellationToken ct = default);
}
