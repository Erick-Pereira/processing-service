using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IProductRepository
{
    Task AddAsync(Product product);
    Task SaveChangesAsync();
}