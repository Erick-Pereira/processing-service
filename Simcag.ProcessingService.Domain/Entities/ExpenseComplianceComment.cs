using System;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>Comentário operacional num achado de conformidade (rastreável).</summary>
public sealed class ExpenseComplianceComment : IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FindingId { get; private set; }
    public Guid ExpenseId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid? AuthorUserId { get; private set; }
    public string? AuthorUserName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ExpenseComplianceComment() { }

    public static ExpenseComplianceComment Create(
        Guid tenantId,
        Guid findingId,
        Guid expenseId,
        string body,
        Guid? authorUserId,
        string? authorUserName)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId obrigatório.");
        if (findingId == Guid.Empty) throw new DomainException("FindingId obrigatório.");
        if (expenseId == Guid.Empty) throw new DomainException("ExpenseId obrigatório.");
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Comentário vazio.");

        return new ExpenseComplianceComment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FindingId = findingId,
            ExpenseId = expenseId,
            Body = body.Trim(),
            AuthorUserId = authorUserId,
            AuthorUserName = string.IsNullOrWhiteSpace(authorUserName) ? null : authorUserName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
