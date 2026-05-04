using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.ReadModel;

namespace Simcag.ProcessingService.Api.Workers;

/// <summary>
/// Refresca a Materialized View <c>mv_monthly_expense_summary</c> em intervalos regulares
/// (default 15 min, configurável via <c>DASHBOARD__REFRESH_INTERVAL_MINUTES</c>).
/// O worker NÃO falha o serviço se a query falhar — apenas loga e tenta de novo.
/// </summary>
public sealed class DashboardRefreshWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DashboardRefreshWorker> _logger;
    private readonly TimeSpan _interval;

    public DashboardRefreshWorker(IServiceProvider services, ILogger<DashboardRefreshWorker> logger)
    {
        _services = services;
        _logger = logger;

        var raw = Environment.GetEnvironmentVariable("DASHBOARD__REFRESH_INTERVAL_MINUTES");
        _interval = int.TryParse(raw, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : TimeSpan.FromMinutes(15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DashboardRefreshWorker iniciado. Intervalo {Interval}", _interval);

        // Espera inicial para a aplicação subir e a migration aplicar a MView.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IDashboardQueryRepository>();
                await repo.RefreshMonthlyExpenseSummaryAsync(stoppingToken);
                _logger.LogDebug("Materialized view mv_monthly_expense_summary atualizada.");
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao refrescar mv_monthly_expense_summary; nova tentativa no próximo ciclo.");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
