using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Simcag.ProcessingService.ReadModel.Models;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.ReadModel;

public sealed class SupplierQualityReadRepository : ISupplierQualityReadModel
{
    private readonly string _connectionString;
    private readonly ITenantContext _tenant;

    public SupplierQualityReadRepository(string connectionString, ITenantContext tenant)
    {
        _connectionString = connectionString;
        _tenant = tenant;
    }

    private NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public async Task<IReadOnlyList<SupplierExpenseStatsRow>> GetExpenseStatsAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT s.id              AS SupplierId,
       s.name            AS SupplierName,
       s.category        AS Category,
       s.is_active       AS IsActive,
       COUNT(e.id)::int  AS ExpenseCount,
       COALESCE(SUM(e.total_amount), 0) AS TotalSpent
FROM suppliers s
LEFT JOIN expenses e
       ON e.supplier_id = s.id
      AND e.tenant_id = s.tenant_id
      AND e.deleted_at IS NULL
WHERE s.tenant_id = @TenantId
GROUP BY s.id, s.name, s.category, s.is_active
ORDER BY TotalSpent DESC, s.name;";

        await using var conn = Open();
        var rows = await conn.QueryAsync<SupplierExpenseStatsRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SupplierComplianceStatsRow>> GetComplianceStatsAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT e.supplier_id AS SupplierId,
       SUM(CASE WHEN f.severity IN ('HIGH', 'CRITICAL') THEN 1 ELSE 0 END)::int AS OpenHighFindings,
       SUM(CASE WHEN f.severity = 'MEDIUM' THEN 1 ELSE 0 END)::int AS OpenMediumFindings,
       SUM(CASE WHEN f.severity = 'LOW' THEN 1 ELSE 0 END)::int AS OpenLowFindings
FROM expense_compliance_findings f
INNER JOIN expenses e ON e.id = f.expense_id AND e.tenant_id = f.tenant_id
WHERE f.tenant_id = @TenantId
  AND f.status = 'OUTSTANDING'
  AND e.deleted_at IS NULL
GROUP BY e.supplier_id;";

        await using var conn = Open();
        var rows = await conn.QueryAsync<SupplierComplianceStatsRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SupplierPriceAuditRow>> GetPriceAuditPayloadsAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT e.supplier_id AS SupplierId,
       a.new_value   AS PayloadJson
FROM audit_logs a
INNER JOIN expenses e ON e.id = a.entity_id AND e.tenant_id = a.tenant_id
WHERE a.tenant_id = @TenantId
  AND a.entity_name = 'Expense'
  AND a.action = 'PriceAnalyzed'
  AND a.new_value IS NOT NULL
  AND e.deleted_at IS NULL;";

        await using var conn = Open();
        var rows = await conn.QueryAsync<SupplierPriceAuditRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId }, cancellationToken: ct));
        return rows.AsList();
    }
}
