using System;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Repositório canônico v1 para a entidade <see cref="Supplier"/>.
/// </summary>
public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Supplier?> GetByCnpjAsync(string cnpj, CancellationToken ct = default);

    Task<Supplier?> GetByNormalizedNameAsync(Guid? condominioId, string normalizedName, CancellationToken ct = default);

    Task<IReadOnlyList<Supplier>> ListAsync(Guid condominioId, string? category, CancellationToken ct = default);

    /// <summary>
    /// Cria ou atualiza um Supplier com base em CNPJ (preferencial) ou NormalizedName + CondominioId.
    /// </summary>
    Task<Supplier> UpsertByCnpjOrNameAsync(
        Guid condominioId,
        string rawName,
        string? cnpj,
        string? category,
        CancellationToken ct = default);
}
