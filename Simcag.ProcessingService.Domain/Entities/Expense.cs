using System;
using System.Collections.Generic;
using System.Linq;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Despesa condominial — agregado raiz canônico.
/// Composta por N <see cref="ExpenseItem"/> (quebra contábil) e N <see cref="Payment"/> (liquidação).
/// Implementa <see cref="IAuditableEntity"/>: cria/aprova/cancela são auditadas automaticamente.
/// </summary>
public sealed class Expense : IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }

    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Currency { get; private set; } = "BRL";

    /// <summary>Data de emissão da nota / documento de origem.</summary>
    public DateTime IssueDate { get; private set; }

    /// <summary>Data de vencimento (para boletos / contas a pagar).</summary>
    public DateTime? DueDate { get; private set; }

    public ExpenseStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Documento bruto que originou esta despesa (idempotência), opcional.</summary>
    public Guid? RawDocumentId { get; private set; }

    /// <summary>Confiança média do enriquecimento IA (0.0-1.0). Opcional.</summary>
    public decimal? ConfidenceScore { get; private set; }

    public bool LowConfidence { get; private set; }

    private readonly List<ExpenseItem> _items = new();
    public IReadOnlyCollection<ExpenseItem> Items => _items.AsReadOnly();

    private readonly List<Payment> _payments = new();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    /// <summary>Total da despesa = soma dos itens. Persistido como coluna calculada via <c>HasComputedColumnSql</c>.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Total já pago (exclui pagamentos estornados).</summary>
    public decimal TotalPaid => _payments.Where(p => !p.IsRefunded).Sum(p => p.Amount);

    /// <summary>Saldo a pagar.</summary>
    public decimal OutstandingBalance => TotalAmount - TotalPaid;

    private Expense() { }

    public static Expense Create(
        Guid tenantId,
        Guid supplierId,
        string description,
        string category,
        DateTime issueDate,
        DateTime? dueDate,
        string currency = "BRL",
        Guid? rawDocumentId = null,
        decimal? confidenceScore = null,
        decimal confidenceThreshold = 0.6m)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId obrigatório.");
        if (supplierId == Guid.Empty) throw new DomainException("SupplierId obrigatório.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Descrição obrigatória.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainException("Categoria obrigatória.");
        if (issueDate > DateTime.UtcNow.AddDays(1))
            throw new DomainException("Data de emissão não pode estar no futuro.");

        var now = DateTime.UtcNow;
        return new Expense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            Description = description.Trim(),
            Category = category.Trim(),
            Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant(),
            IssueDate = issueDate,
            DueDate = dueDate,
            Status = ExpenseStatus.Pending,
            RawDocumentId = rawDocumentId,
            ConfidenceScore = confidenceScore,
            LowConfidence = confidenceScore.HasValue && confidenceScore.Value < confidenceThreshold,
            CreatedAt = now,
            UpdatedAt = now,
            TotalAmount = 0m,
        };
    }

    public ExpenseItem AddItem(string description, decimal quantity, decimal unitPrice)
    {
        EnsureMutable();
        if (Status != ExpenseStatus.Pending)
            throw new DomainException("Itens só podem ser alterados quando a despesa está Pending.");

        var item = ExpenseItem.Create(Id, description, quantity, unitPrice);
        _items.Add(item);
        Recalculate();
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureMutable();
        if (Status != ExpenseStatus.Pending)
            throw new DomainException("Itens só podem ser alterados quando a despesa está Pending.");

        var item = _items.FirstOrDefault(i => i.Id == itemId)
                   ?? throw new DomainException($"Item {itemId} não encontrado.");
        _items.Remove(item);
        Recalculate();
    }

    public void Approve()
    {
        EnsureMutable();
        if (Status != ExpenseStatus.Pending)
            throw new DomainException("Apenas despesas Pending podem ser aprovadas.");
        if (_items.Count == 0)
            throw new DomainException("Não é possível aprovar despesa sem itens.");

        Status = ExpenseStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        EnsureMutable();
        if (Status == ExpenseStatus.Paid)
            throw new DomainException("Despesas já pagas não podem ser canceladas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Motivo do cancelamento é obrigatório.");

        Status = ExpenseStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public Payment RegisterPayment(decimal amount, DateTime date, PaymentMethod method, string? referenceCode = null)
    {
        EnsureMutable();
        if (Status != ExpenseStatus.Approved && Status != ExpenseStatus.Paid)
            throw new DomainException("Apenas despesas aprovadas podem receber pagamentos.");
        if (amount <= 0m) throw new DomainException("Valor do pagamento deve ser > 0.");
        if (amount > OutstandingBalance + 0.001m)
            throw new DomainException($"Pagamento ({amount:0.00}) excede o saldo devedor ({OutstandingBalance:0.00}).");

        var payment = Payment.Create(TenantId, Id, amount, date, method, referenceCode);
        _payments.Add(payment);

        if (OutstandingBalance <= 0.001m)
        {
            Status = ExpenseStatus.Paid;
        }
        UpdatedAt = DateTime.UtcNow;
        return payment;
    }

    public void RefundPayment(Guid paymentId, string reason)
    {
        EnsureMutable();
        var payment = _payments.FirstOrDefault(p => p.Id == paymentId)
                      ?? throw new DomainException($"Pagamento {paymentId} não encontrado nesta despesa.");
        payment.Refund(reason);

        if (Status == ExpenseStatus.Paid && OutstandingBalance > 0.001m)
        {
            Status = ExpenseStatus.Approved;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue) return;
        if (_payments.Any(p => !p.IsRefunded))
            throw new DomainException("Não é possível excluir despesa com pagamentos ativos.");
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Recalculate()
    {
        TotalAmount = _items.Sum(i => i.TotalPrice);
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureMutable()
    {
        if (DeletedAt.HasValue)
            throw new DomainException("Despesa excluída não pode ser modificada.");
    }
}
