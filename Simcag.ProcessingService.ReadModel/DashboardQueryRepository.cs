using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Simcag.ProcessingService.ReadModel.Models;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.ReadModel;

/// <summary>
/// Implementação Dapper. Lê da MView <c>mv_monthly_expense_summary</c> (rápido, sem tracking).
/// Para gráficos em tempo real (sem o lag da MView), o callsite pode tocar a função SQL
/// <c>refresh_monthly_expense_summary()</c> via <see cref="RefreshMonthlyExpenseSummaryAsync"/>.
/// </summary>
public sealed class DashboardQueryRepository : IDashboardQueryRepository
{
    private readonly string _connectionString;
    private readonly ITenantContext _tenant;

    public DashboardQueryRepository(string connectionString, ITenantContext tenant)
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

    public async Task<IReadOnlyList<MonthlyExpenseSummaryRow>> GetMonthlySummaryAsync(int year, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tenant_id        AS TenantId,
       year             AS Year,
       month            AS Month,
       category         AS Category,
       supplier_id      AS SupplierId,
       expense_count    AS ExpenseCount,
       total_amount     AS TotalAmount,
       total_paid       AS TotalPaid,
       outstanding      AS Outstanding
FROM mv_monthly_expense_summary
WHERE tenant_id = @TenantId AND year = @Year
ORDER BY month, category;";
        await using var conn = Open();
        var rows = await conn.QueryAsync<MonthlyExpenseSummaryRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId, Year = year }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CategoryBreakdownRow>> GetCategoryBreakdownAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = @"
SELECT category                            AS Category,
       SUM(expense_count)::int             AS ExpenseCount,
       COALESCE(SUM(total_amount), 0)      AS TotalAmount,
       COALESCE(SUM(total_paid), 0)        AS TotalPaid
FROM mv_monthly_expense_summary
WHERE tenant_id = @TenantId
  AND make_date(year, month, 1) BETWEEN date_trunc('month', @From::date) AND date_trunc('month', @To::date)
GROUP BY category
ORDER BY TotalAmount DESC;";
        await using var conn = Open();
        var rows = await conn.QueryAsync<CategoryBreakdownRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId, From = from, To = to }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SupplierRankingRow>> GetSupplierRankingAsync(DateTime from, DateTime to, int top, CancellationToken ct = default)
    {
        if (top <= 0) top = 10;
        const string sql = @"
SELECT mv.supplier_id                    AS SupplierId,
       COALESCE(s.name, '(desconhecido)') AS SupplierName,
       SUM(mv.expense_count)::int        AS ExpenseCount,
       COALESCE(SUM(mv.total_amount), 0) AS TotalAmount
FROM mv_monthly_expense_summary mv
LEFT JOIN suppliers s ON s.id = mv.supplier_id AND s.tenant_id = mv.tenant_id
WHERE mv.tenant_id = @TenantId
  AND make_date(mv.year, mv.month, 1) BETWEEN date_trunc('month', @From::date) AND date_trunc('month', @To::date)
GROUP BY mv.supplier_id, s.name
ORDER BY TotalAmount DESC
LIMIT @Top;";
        await using var conn = Open();
        var rows = await conn.QueryAsync<SupplierRankingRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId, From = from, To = to, Top = top }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CashFlowRow>> GetCashFlowAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = @"
WITH scheduled AS (
    SELECT EXTRACT(YEAR FROM e.due_date)::int  AS year,
           EXTRACT(MONTH FROM e.due_date)::int AS month,
           SUM(e.total_amount)                 AS scheduled_amount
    FROM expenses e
    WHERE e.tenant_id = @TenantId
      AND e.deleted_at IS NULL
      AND e.due_date IS NOT NULL
      AND e.due_date BETWEEN @From AND @To
    GROUP BY 1, 2
),
paid AS (
    SELECT EXTRACT(YEAR FROM p.payment_date)::int  AS year,
           EXTRACT(MONTH FROM p.payment_date)::int AS month,
           SUM(p.amount)                           AS paid_amount
    FROM payments p
    WHERE p.tenant_id = @TenantId
      AND p.is_refunded = FALSE
      AND p.payment_date BETWEEN @From AND @To
    GROUP BY 1, 2
)
SELECT COALESCE(s.year,  p.year)  AS Year,
       COALESCE(s.month, p.month) AS Month,
       COALESCE(s.scheduled_amount, 0) AS ScheduledAmount,
       COALESCE(p.paid_amount, 0)      AS PaidAmount
FROM scheduled s
FULL OUTER JOIN paid p ON s.year = p.year AND s.month = p.month
ORDER BY 1, 2;";
        await using var conn = Open();
        var rows = await conn.QueryAsync<CashFlowRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId, From = from, To = to }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<YearOverYearRow>> GetYearOverYearAsync(int yearsBack, CancellationToken ct = default)
    {
        if (yearsBack <= 0) yearsBack = 2;
        const string sql = @"
SELECT year   AS Year,
       month  AS Month,
       COALESCE(SUM(total_amount), 0) AS TotalAmount
FROM mv_monthly_expense_summary
WHERE tenant_id = @TenantId AND year >= EXTRACT(YEAR FROM CURRENT_DATE)::int - @YearsBack
GROUP BY year, month
ORDER BY year, month;";
        await using var conn = Open();
        var rows = await conn.QueryAsync<YearOverYearRow>(
            new CommandDefinition(sql, new { TenantId = _tenant.TenantId, YearsBack = yearsBack }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task RefreshMonthlyExpenseSummaryAsync(CancellationToken ct = default)
    {
        await using var conn = Open();
        await conn.ExecuteAsync(new CommandDefinition(
            "SELECT refresh_monthly_expense_summary();",
            cancellationToken: ct));
    }
}
