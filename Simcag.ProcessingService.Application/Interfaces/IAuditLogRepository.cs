using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>Insere um registro de auditoria explícito (ex.: eventos assíncronos) e persiste.</summary>
    Task AppendAsync(AuditLog log, CancellationToken ct = default);

    Task<(IReadOnlyList<AuditLog> Items, int Total)> ListAsync(
        string? entityName,
        Guid? entityId,
        Guid? performedBy,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default);
}
