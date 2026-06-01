using System;

namespace Simcag.ProcessingService.ReadModel.Models;

public sealed class SupplierExpenseStatsRow
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalSpent { get; set; }
}

public sealed class SupplierComplianceStatsRow
{
    public Guid SupplierId { get; set; }
    public int OpenHighFindings { get; set; }
    public int OpenMediumFindings { get; set; }
    public int OpenLowFindings { get; set; }
}

public sealed class SupplierPriceAuditRow
{
    public Guid SupplierId { get; set; }
    public string? PayloadJson { get; set; }
}
