using System.Collections.Generic;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Infrastructure.Auditing;

/// <summary>
/// Buffer scoped que recebe entradas do <see cref="AuditSaveChangesInterceptor"/>
/// e as expõe para o <c>ProcessingDbContext</c> persistir como <see cref="AuditLog"/>
/// na mesma transação do <c>SaveChangesAsync</c>.
/// </summary>
public sealed class ProcessingAuditLogSink : IAuditLogSink
{
    private readonly List<AuditEntry> _pending = new();

    public void AddRange(IReadOnlyCollection<AuditEntry> entries)
    {
        if (entries.Count == 0) return;
        _pending.AddRange(entries);
    }

    /// <summary>Retorna e limpa a lista pendente — chamado pelo DbContext durante <c>SaveChangesAsync</c>.</summary>
    public IReadOnlyList<AuditEntry> Drain()
    {
        if (_pending.Count == 0) return System.Array.Empty<AuditEntry>();
        var snapshot = _pending.ToArray();
        _pending.Clear();
        return snapshot;
    }
}
