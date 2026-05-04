using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.ReadModel.Models;

namespace Simcag.ProcessingService.ReadModel;

/// <summary>
/// Read-side de dashboards. Implementação Dapper consulta a Materialized View
/// <c>mv_monthly_expense_summary</c> e tabelas auxiliares.
/// Sempre filtrado por tenant atual (resolvido via <c>ITenantContext</c>).
/// </summary>
public interface IDashboardQueryRepository
{
    Task<IReadOnlyList<MonthlyExpenseSummaryRow>> GetMonthlySummaryAsync(int year, CancellationToken ct = default);

    Task<IReadOnlyList<CategoryBreakdownRow>> GetCategoryBreakdownAsync(DateTime from, DateTime to, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierRankingRow>> GetSupplierRankingAsync(DateTime from, DateTime to, int top, CancellationToken ct = default);

    Task<IReadOnlyList<CashFlowRow>> GetCashFlowAsync(DateTime from, DateTime to, CancellationToken ct = default);

    Task<IReadOnlyList<YearOverYearRow>> GetYearOverYearAsync(int yearsBack, CancellationToken ct = default);

    Task RefreshMonthlyExpenseSummaryAsync(CancellationToken ct = default);
}
