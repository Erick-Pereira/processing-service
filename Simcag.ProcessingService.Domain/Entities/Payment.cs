using System;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Pagamento (parcial ou total) realizado contra uma <see cref="Expense"/>.
/// Aggregate root próprio para permitir queries por período sem carregar Expense.
/// Implementa <see cref="IAuditableEntity"/>: toda mudança é registrada em <c>audit_logs</c>.
/// </summary>
public sealed class Payment : IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ExpenseId { get; private set; }

    public decimal Amount { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public PaymentMethod Method { get; private set; }

    /// <summary>Identificador externo (txid PIX, número de boleto, etc.) — opcional.</summary>
    public string? ReferenceCode { get; private set; }

    public bool IsRefunded { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public string? RefundReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Payment() { }

    internal static Payment Create(
        Guid tenantId,
        Guid expenseId,
        decimal amount,
        DateTime paymentDate,
        PaymentMethod method,
        string? referenceCode)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId obrigatório.");
        if (expenseId == Guid.Empty) throw new DomainException("ExpenseId obrigatório.");
        if (amount <= 0m) throw new DomainException("Valor do pagamento deve ser > 0.");
        if (paymentDate > DateTime.UtcNow.AddDays(1))
            throw new DomainException("Data do pagamento não pode estar no futuro.");

        return new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExpenseId = expenseId,
            Amount = amount,
            PaymentDate = paymentDate,
            Method = method,
            ReferenceCode = string.IsNullOrWhiteSpace(referenceCode) ? null : referenceCode.Trim(),
            IsRefunded = false,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Refund(string reason)
    {
        if (IsRefunded) throw new DomainException("Pagamento já foi estornado.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Motivo do estorno é obrigatório.");
        IsRefunded = true;
        RefundedAt = DateTime.UtcNow;
        RefundReason = reason.Trim();
    }
}
