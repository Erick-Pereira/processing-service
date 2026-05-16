using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IOperationalInsightSnapshotRepository
{
    Task<OperationalInsightSnapshot?> GetLatestValidAsync(
        Guid tenantId,
        DateTime utcNow,
        string ruleSetVersion,
        CancellationToken ct = default);

    Task AddAsync(OperationalInsightSnapshot snapshot, CancellationToken ct = default);

    Task<IReadOnlyList<OperationalInsightSnapshot>> ListRecentAsync(
        Guid tenantId,
        int take,
        CancellationToken ct = default);
}
