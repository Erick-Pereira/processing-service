using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Application.UseCases.AuditLogs;

public sealed record ListAuditLogsQuery(
    string? EntityName,
    Guid? EntityId,
    Guid? PerformedBy,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<PagedResult<AuditLogDto>>;

public sealed class ListAuditLogsHandler : IRequestHandler<ListAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IAuditLogRepository _auditLogs;
    public ListAuditLogsHandler(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<PagedResult<AuditLogDto>> Handle(ListAuditLogsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var (items, total) = await _auditLogs.ListAsync(q.EntityName, q.EntityId, q.PerformedBy, q.From, q.To,
            (page - 1) * size, size, ct);

        return new PagedResult<AuditLogDto>(
            items.Select(a => new AuditLogDto
            {
                Id = a.Id,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Action = a.Action,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                PerformedBy = a.PerformedBy,
                PerformedByName = a.PerformedByName,
                CreatedAt = a.CreatedAt,
            }).ToList(), total, page, size);
    }
}
