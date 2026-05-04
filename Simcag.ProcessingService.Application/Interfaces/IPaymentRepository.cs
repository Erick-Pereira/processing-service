using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Payment>> ListByExpenseAsync(Guid expenseId, CancellationToken ct = default);

    Task<(IReadOnlyList<Payment> Items, int Total)> ListAsync(
        Guid? expenseId,
        DateTime? from,
        DateTime? to,
        bool? refunded,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Marca explicitamente o Payment como Added.
    /// Necessário porque o EF Core, ao detectar uma nova entidade dentro de uma navigation
    /// field-backed em um agregado já carregado, com PK manualmente atribuída (Guid.NewGuid),
    /// pode marcá-la como Modified em vez de Added.
    /// </summary>
    Task AddAsync(Payment payment, CancellationToken ct = default);
}
