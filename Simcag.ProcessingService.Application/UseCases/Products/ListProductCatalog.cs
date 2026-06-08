using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Application.UseCases.Products;

public sealed record ListProductCatalogQuery(
    string? Query,
    string? Category,
    Guid? SupplierId,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize,
    int MaxSourceRows) : IRequest<ProductCatalogResultDto>;

public sealed class ListProductCatalogHandler : IRequestHandler<ListProductCatalogQuery, ProductCatalogResultDto>
{
    private const int RecentMovementsLimit = 5;
    private const int SuppliersLimit = 5;

    private readonly IProductCatalogQueryRepository _catalog;
    private readonly IProductRepository _products;

    public ListProductCatalogHandler(
        IProductCatalogQueryRepository catalog,
        IProductRepository products)
    {
        _catalog = catalog;
        _products = products;
    }

    public async Task<ProductCatalogResultDto> Handle(ListProductCatalogQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var maxSourceRows = Math.Clamp(request.MaxSourceRows <= 0 ? 5000 : request.MaxSourceRows, 100, 10000);

        var sourceRows = await _catalog.ListSourceRowsAsync(
            new ProductCatalogFilters(
                request.Query,
                request.Category,
                request.SupplierId,
                request.From,
                request.To,
                maxSourceRows + 1),
            ct);

        var limitedRows = sourceRows.Take(maxSourceRows).ToList();
        var grouped = limitedRows
            .GroupBy(row => new
            {
                NormalizedName = ProductCatalogNormalizer.Normalize(row.Description),
                Category = string.IsNullOrWhiteSpace(row.Category) ? "Sem categoria" : row.Category.Trim(),
            })
            .ToList();

        var catalogKeys = grouped.Select(g => g.Key.NormalizedName).Distinct(StringComparer.OrdinalIgnoreCase);
        var benchmarks = await _products.GetLatestBenchmarksByCatalogKeysAsync(catalogKeys, ct);

        var groups = grouped
            .Select(group =>
            {
                benchmarks.TryGetValue(group.Key.NormalizedName, out var benchmark);
                return BuildCatalogItem(group.Key.NormalizedName, group.Key.Category, group, benchmark);
            })
            .OrderByDescending(item => item.LastSeen)
            .ThenBy(item => item.DisplayName)
            .ToList();

        return new ProductCatalogResultDto
        {
            Items = groups.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Total = groups.Count,
            Page = page,
            PageSize = pageSize,
            SourceRows = Math.Min(sourceRows.Count, maxSourceRows),
            IsLimited = sourceRows.Count > maxSourceRows,
        };
    }

    private static ProductCatalogItemDto BuildCatalogItem(
        string normalizedName,
        string category,
        IEnumerable<ProductCatalogSourceRow> rows,
        ProductBenchmarkSnapshot? benchmark)
    {
        var orderedRows = rows
            .OrderByDescending(row => row.IssueDate)
            .ThenBy(row => row.Description)
            .ToList();
        var unitPrices = orderedRows.Select(row => row.UnitPrice).Where(price => price > 0m).ToList();
        var minUnitPrice = unitPrices.Count == 0 ? 0m : unitPrices.Min();
        var maxUnitPrice = unitPrices.Count == 0 ? 0m : unitPrices.Max();

        return new ProductCatalogItemDto
        {
            ProductKey = $"{normalizedName}|{ProductCatalogNormalizer.Normalize(category)}",
            DisplayName = SelectDisplayName(orderedRows),
            NormalizedName = normalizedName,
            Category = category,
            OccurrenceCount = orderedRows.Count,
            ExpenseCount = orderedRows.Select(row => row.ExpenseId).Distinct().Count(),
            SupplierCount = orderedRows.Select(row => row.SupplierId).Distinct().Count(),
            TotalQuantity = orderedRows.Sum(row => row.Quantity),
            TotalSpent = orderedRows.Sum(row => row.TotalPrice),
            AverageUnitPrice = unitPrices.Count == 0 ? 0m : Math.Round(unitPrices.Average(), 2),
            MinUnitPrice = minUnitPrice,
            MaxUnitPrice = maxUnitPrice,
            VariationPercentage = minUnitPrice > 0m
                ? Math.Round(((maxUnitPrice - minUnitPrice) / minUnitPrice) * 100m, 2)
                : null,
            MarketBenchmarkPrice = benchmark?.MarketBenchmarkPrice,
            MarketDeviationPercentage = benchmark?.MarketDeviationPercentage,
            LastBenchmarkAt = benchmark?.BenchmarkAt,
            FirstSeen = orderedRows.Min(row => row.IssueDate),
            LastSeen = orderedRows.Max(row => row.IssueDate),
            Suppliers = orderedRows
                .GroupBy(row => new { row.SupplierId, row.SupplierName })
                .Select(group =>
                {
                    var supplierPrices = group.Select(row => row.UnitPrice).Where(price => price > 0m).ToList();
                    return new ProductSupplierSummaryDto
                    {
                        SupplierId = group.Key.SupplierId,
                        SupplierName = string.IsNullOrWhiteSpace(group.Key.SupplierName)
                            ? "Fornecedor não identificado"
                            : group.Key.SupplierName,
                        OccurrenceCount = group.Count(),
                        AverageUnitPrice = supplierPrices.Count == 0 ? 0m : Math.Round(supplierPrices.Average(), 2),
                        MinUnitPrice = supplierPrices.Count == 0 ? 0m : supplierPrices.Min(),
                        MaxUnitPrice = supplierPrices.Count == 0 ? 0m : supplierPrices.Max(),
                        TotalSpent = group.Sum(row => row.TotalPrice),
                        LastSeen = group.Max(row => row.IssueDate),
                    };
                })
                .OrderByDescending(item => item.TotalSpent)
                .ThenBy(item => item.SupplierName)
                .Take(SuppliersLimit)
                .ToList(),
            RecentMovements = orderedRows
                .Take(RecentMovementsLimit)
                .Select(row => new ProductMovementDto
                {
                    ExpenseId = row.ExpenseId,
                    ExpenseItemId = row.ExpenseItemId,
                    SupplierId = row.SupplierId,
                    SupplierName = string.IsNullOrWhiteSpace(row.SupplierName)
                        ? "Fornecedor não identificado"
                        : row.SupplierName,
                    ItemDescription = string.IsNullOrWhiteSpace(row.Description)
                        ? "Linha sem descrição"
                        : row.Description.Trim(),
                    ExpenseDescription = string.IsNullOrWhiteSpace(row.ExpenseDescription)
                        ? string.Empty
                        : row.ExpenseDescription.Trim(),
                    IssueDate = row.IssueDate,
                    Quantity = row.Quantity,
                    UnitPrice = row.UnitPrice,
                    TotalPrice = row.TotalPrice,
                })
                .ToList(),
        };
    }

    private static string SelectDisplayName(IReadOnlyList<ProductCatalogSourceRow> rows)
    {
        return rows
            .GroupBy(row => row.Description.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(row => row.IssueDate))
            .Select(group => group.Key)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "Produto sem descrição";
    }
}
