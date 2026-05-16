using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class ProductCatalogQueryRepository : IProductCatalogQueryRepository
{
    private readonly ProcessingDbContext _db;

    public ProductCatalogQueryRepository(ProcessingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductCatalogSourceRow>> ListSourceRowsAsync(
        ProductCatalogFilters filters,
        CancellationToken ct = default)
    {
        var query =
            from item in _db.ExpenseItems.AsNoTracking()
            join expense in _db.Expenses.AsNoTracking() on item.ExpenseId equals expense.Id
            join supplier in _db.Suppliers.AsNoTracking() on expense.SupplierId equals supplier.Id into supplierJoin
            from supplier in supplierJoin.DefaultIfEmpty()
            select new { item, expense, supplier };

        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            var pattern = $"%{filters.Query.Trim()}%";
            query = query.Where(row =>
                EF.Functions.ILike(row.item.Description, pattern) ||
                EF.Functions.ILike(row.expense.Description, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filters.Category))
        {
            var category = filters.Category.Trim();
            query = query.Where(row => row.expense.Category == category);
        }

        if (filters.SupplierId.HasValue)
            query = query.Where(row => row.expense.SupplierId == filters.SupplierId.Value);

        if (filters.From.HasValue)
            query = query.Where(row => row.expense.IssueDate >= filters.From.Value);

        if (filters.To.HasValue)
            query = query.Where(row => row.expense.IssueDate <= filters.To.Value);

        return await query
            .OrderByDescending(row => row.expense.IssueDate)
            .ThenBy(row => row.item.Description)
            .Take(Math.Clamp(filters.MaxSourceRows, 100, 10001))
            .Select(row => new ProductCatalogSourceRow(
                row.expense.Id,
                row.item.Id,
                row.expense.SupplierId,
                row.supplier == null ? string.Empty : row.supplier.Name,
                row.item.Description,
                row.expense.Category,
                row.expense.IssueDate,
                row.item.Quantity,
                row.item.UnitPrice,
                row.item.TotalPrice,
                row.expense.Description ?? string.Empty))
            .ToListAsync(ct);
    }
}
