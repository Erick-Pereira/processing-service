using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.ReadModel.Models;

namespace Simcag.ProcessingService.ReadModel;

public interface ISupplierQualityReadModel
{
    Task<IReadOnlyList<SupplierExpenseStatsRow>> GetExpenseStatsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SupplierComplianceStatsRow>> GetComplianceStatsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SupplierPriceAuditRow>> GetPriceAuditPayloadsAsync(CancellationToken ct = default);
}
