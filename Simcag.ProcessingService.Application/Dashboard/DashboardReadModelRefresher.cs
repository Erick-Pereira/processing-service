using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.ReadModel;

namespace Simcag.ProcessingService.Application.Dashboard;

public sealed class DashboardReadModelRefresher : IDashboardReadModelRefresher
{
    private readonly IDashboardQueryRepository _dashboard;
    private readonly ILogger<DashboardReadModelRefresher> _logger;

    public DashboardReadModelRefresher(
        IDashboardQueryRepository dashboard,
        ILogger<DashboardReadModelRefresher> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    public async Task RefreshAfterExpenseMutationAsync(CancellationToken ct = default)
    {
        try
        {
            await _dashboard.RefreshMonthlyExpenseSummaryAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao refrescar mv_monthly_expense_summary após mutação de despesa.");
        }
    }
}
