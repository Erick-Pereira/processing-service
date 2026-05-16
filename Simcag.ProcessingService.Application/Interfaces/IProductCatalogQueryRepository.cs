using Simcag.ProcessingService.Application.DTOs;

namespace Simcag.ProcessingService.Application.Interfaces;

public sealed record ProductCatalogFilters(
    string? Query,
    string? Category,
    Guid? SupplierId,
    DateTime? From,
    DateTime? To,
    int MaxSourceRows);

public interface IProductCatalogQueryRepository
{
    Task<IReadOnlyList<ProductCatalogSourceRow>> ListSourceRowsAsync(
        ProductCatalogFilters filters,
        CancellationToken ct = default);
}
