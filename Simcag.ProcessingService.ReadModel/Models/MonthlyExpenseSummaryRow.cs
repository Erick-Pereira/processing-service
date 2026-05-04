using System;

namespace Simcag.ProcessingService.ReadModel.Models;

public sealed class MonthlyExpenseSummaryRow
{
    public Guid TenantId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Category { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Outstanding { get; set; }
}

public sealed class CategoryBreakdownRow
{
    public string Category { get; set; } = string.Empty;
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
}

public sealed class SupplierRankingRow
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class CashFlowRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ScheduledAmount { get; set; }
    public decimal PaidAmount { get; set; }
}

public sealed class YearOverYearRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalAmount { get; set; }
}
