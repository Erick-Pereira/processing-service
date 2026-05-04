using System;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Registro imutável de auditoria. Persistido pela mesma transação do <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync"/>
/// via <c>Simcag.Shared.Auditing.AuditSaveChangesInterceptor</c>.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;

    /// <summary>JSON serializado do estado anterior (NULL para CREATE).</summary>
    public string? OldValue { get; private set; }

    /// <summary>JSON serializado do estado novo (NULL para DELETE).</summary>
    public string? NewValue { get; private set; }

    public Guid? PerformedBy { get; private set; }
    public string? PerformedByName { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private AuditLog() { }

    internal static AuditLog Create(
        Guid tenantId,
        string entityName,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        Guid? performedBy,
        string? performedByName,
        DateTime createdAt)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            PerformedBy = performedBy,
            PerformedByName = performedByName,
            CreatedAt = createdAt,
        };
    }

    /// <summary>Factory pública usada pelo sink/serviço para mapear AuditEntry → AuditLog.</summary>
    public static AuditLog FromEntry(
        Guid tenantId,
        string entityName,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        Guid? performedBy,
        string? performedByName,
        DateTime createdAt) =>
        Create(tenantId, entityName, entityId, action, oldValue, newValue, performedBy, performedByName, createdAt);
}
