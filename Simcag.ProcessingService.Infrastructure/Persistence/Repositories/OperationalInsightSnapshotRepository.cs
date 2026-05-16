using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Infrastructure.Persistence;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class OperationalInsightSnapshotRepository : IOperationalInsightSnapshotRepository
{
    private readonly ProcessingDbContext _db;

    public OperationalInsightSnapshotRepository(ProcessingDbContext db) => _db = db;

    public async Task<OperationalInsightSnapshot?> GetLatestValidAsync(
        Guid tenantId,
        DateTime utcNow,
        string ruleSetVersion,
        CancellationToken ct = default)
    {
        return await _db.Set<OperationalInsightSnapshot>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                        && x.RuleSetVersion == ruleSetVersion
                        && x.ExpiresAtUtc > utcNow)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(OperationalInsightSnapshot snapshot, CancellationToken ct = default)
    {
        await _db.Set<OperationalInsightSnapshot>().AddAsync(snapshot, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OperationalInsightSnapshot>> ListRecentAsync(
        Guid tenantId,
        int take,
        CancellationToken ct = default)
    {
        if (take < 1) take = 1;
        if (take > 200) take = 200;

        return await _db.Set<OperationalInsightSnapshot>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }
}
