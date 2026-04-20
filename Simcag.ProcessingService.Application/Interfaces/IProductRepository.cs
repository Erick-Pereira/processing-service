using Simcag.ProcessingService.Domain.Entities;
using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IProductRepository
{
    Task<Product> AddAsync(Product product, CancellationToken cancellationToken);
    Task<Product?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);
    Task<Product> UpdateAsync(Product product, CancellationToken cancellationToken);
}
