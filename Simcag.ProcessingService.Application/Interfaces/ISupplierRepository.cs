using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<Supplier?> GetByDocumentAsync(string document, CancellationToken ct = default);

    Task<Supplier?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default);

    Task<IReadOnlyList<Supplier>> ListAsync(string? category, CancellationToken ct = default);

    Task AddAsync(Supplier supplier, CancellationToken ct = default);

    /// <summary>
    /// Cria ou atualiza um Supplier com base em Documento (preferencial) ou NormalizedName,
    /// dentro do escopo do tenant atual (resolvido via <c>ITenantContext</c>).
    /// </summary>
    Task<Supplier> UpsertByDocumentOrNameAsync(
        string name,
        string document,
        string? category,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
