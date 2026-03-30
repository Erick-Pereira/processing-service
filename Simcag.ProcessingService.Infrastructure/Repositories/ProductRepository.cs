using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Infrastructure.Persistence;

namespace Simcag.ProcessingService.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ProcessingDbContext _dbContext;

    public ProductRepository(ProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Product product)
    {
        await _dbContext.Products.AddAsync(product);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}