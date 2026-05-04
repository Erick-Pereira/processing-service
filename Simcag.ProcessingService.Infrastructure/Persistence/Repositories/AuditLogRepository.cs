using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ProcessingDbContext _db;

    public AuditLogRepository(ProcessingDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> ListAsync(
        string? entityName,
        Guid? entityId,
        Guid? performedBy,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityName)) q = q.Where(a => a.EntityName == entityName);
        if (entityId.HasValue) q = q.Where(a => a.EntityId == entityId);
        if (performedBy.HasValue) q = q.Where(a => a.PerformedBy == performedBy);
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedAt <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }
}
