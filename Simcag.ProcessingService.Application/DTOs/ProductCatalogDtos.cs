using System;
using System.Collections.Generic;

namespace Simcag.ProcessingService.Application.DTOs;

public sealed class ProductCatalogResultDto
{
    public IReadOnlyList<ProductCatalogItemDto> Items { get; init; } = Array.Empty<ProductCatalogItemDto>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int SourceRows { get; init; }
    public bool IsLimited { get; init; }
}

public sealed class ProductCatalogItemDto
{
    public string ProductKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int OccurrenceCount { get; init; }
    public int ExpenseCount { get; init; }
    public int SupplierCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal AverageUnitPrice { get; init; }
    public decimal MinUnitPrice { get; init; }
    public decimal MaxUnitPrice { get; init; }
    public decimal? VariationPercentage { get; init; }
    public decimal? MarketBenchmarkPrice { get; init; }
    public decimal? MarketDeviationPercentage { get; init; }
    public DateTime? LastBenchmarkAt { get; init; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public IReadOnlyList<ProductSupplierSummaryDto> Suppliers { get; init; } = Array.Empty<ProductSupplierSummaryDto>();
    public IReadOnlyList<ProductMovementDto> RecentMovements { get; init; } = Array.Empty<ProductMovementDto>();
}

public sealed class ProductSupplierSummaryDto
{
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public int OccurrenceCount { get; init; }
    public decimal AverageUnitPrice { get; init; }
    public decimal MinUnitPrice { get; init; }
    public decimal MaxUnitPrice { get; init; }
    public decimal TotalSpent { get; init; }
    public DateTime LastSeen { get; init; }
}

public sealed class ProductMovementDto
{
    public Guid ExpenseId { get; init; }
    public Guid ExpenseItemId { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    /// <summary>Descrição da linha na nota (item).</summary>
    public string ItemDescription { get; init; } = string.Empty;
    /// <summary>Descrição ou título da despesa/nota (contexto agregado).</summary>
    public string ExpenseDescription { get; init; } = string.Empty;
    public DateTime IssueDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalPrice { get; init; }
}

public sealed record ProductCatalogSourceRow(
    Guid ExpenseId,
    Guid ExpenseItemId,
    Guid SupplierId,
    string SupplierName,
    string Description,
    string Category,
    DateTime IssueDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string ExpenseDescription);
