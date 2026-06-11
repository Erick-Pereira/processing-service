using System.Threading;
using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Dashboard;

/// <summary>Atualiza a MView do dashboard após mutações que alteram saldo em aberto.</summary>
public interface IDashboardReadModelRefresher
{
    Task RefreshAfterExpenseMutationAsync(CancellationToken ct = default);
}
