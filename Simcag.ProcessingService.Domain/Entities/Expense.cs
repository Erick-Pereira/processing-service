using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.ProcessingService.Domain.StateMachine;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Despesa condominial — agregado raiz canônico.
/// Composta por N <see cref="ExpenseItem"/> (quebra contábil) e N <see cref="Payment"/> (liquidação).
/// Separação explícita: <see cref="ProcessingStatus"/> (pipeline técnica) vs <see cref="ApprovalStatus"/> (humano)
/// vs <see cref="SettlementStatus"/> (pagamentos). O campo <see cref="Status"/> permanece como espelho legado
/// para índices e APIs que filtram pelo enum histórico.
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

    /// <summary>Espelho legado sincronizado com aprovação + liquidação + falhas de processamento.</summary>
    public ExpenseStatus Status { get; private set; }

    /// <summary>Estado da pipeline técnica (ingestão, benchmark, …).</summary>
    public ExpenseProcessingStatus ProcessingStatus { get; private set; }

    /// <summary>Estado da aprovação humana.</summary>
    public ExpenseApprovalStatus ApprovalStatus { get; private set; }

    /// <summary>Liquidação (pagamentos).</summary>
    public ExpenseSettlementStatus SettlementStatus { get; private set; }

    /// <summary>Motivo da última falha de processamento (quando <see cref="ProcessingStatus"/> é <see cref="ExpenseProcessingStatus.Failed"/>).</summary>
    public string? ProcessingFailureReason { get; private set; }

    public DateTime? ProcessingFailedAt { get; private set; }

    /// <summary>Número de retries explícitos da pipeline (via <see cref="RetryProcessingPipeline"/>).</summary>
    public int ProcessingRetryCount { get; private set; }

    /// <summary>Última transição de processamento (auditoria / observabilidade).</summary>
    public DateTime? LastPipelineTransitionAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Documento bruto que originou esta despesa (idempotência), opcional.</summary>
    public Guid? RawDocumentId { get; private set; }

    /// <summary>Confiança média do enriquecimento IA (0.0-1.0). Opcional.</summary>
    public decimal? ConfidenceScore { get; private set; }

    public bool LowConfidence { get; private set; }

    // --- OTIMIZAÇÃO 3: Capacidades explícitas para evitar realocação automática (default capacity 16) ---
    [NotMapped]
    private readonly List<ExpenseItem> _items = new(8);

    private readonly List<Payment> _payments = new(4);

    public IReadOnlyCollection<ExpenseItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    /// <summary>Total da despesa = soma dos itens. Persistido como coluna calculada via <c>HasComputedColumnSql</c>.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Total já pago (exclui pagamentos estornados). Usa inteiros para precisão monetária.</summary>
    public decimal TotalPaid => _payments.Where(p => !p.IsRefunded).Sum(p => p.Amount);

    /// <summary>Saldo a pagar.</summary>
    public decimal OutstandingBalance => TotalAmount - TotalPaid;

    private Expense() { }

    /// <param name="fromAsyncDocumentIngest">
    /// <see langword="true"/>: despesa criada pela fila de ingestão (pipeline ainda em curso, começa em <see cref="ExpenseProcessingStatus.Received"/>).
    /// <see langword="false"/>: cadastro manual já completo do ponto de vista técnico (<see cref="ExpenseProcessingStatus.Completed"/>).
    /// </param>
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
        decimal confidenceThreshold = 0.6m,
        bool fromAsyncDocumentIngest = false)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId obrigatório.");
        if (supplierId == Guid.Empty) throw new DomainException("SupplierId obrigatório.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Descrição obrigatória.");
        if (string.IsNullOrWhiteSpace(category)) throw new DomainException("Categoria obrigatória.");
        if (issueDate > DateTime.UtcNow.AddDays(1))
            throw new DomainException("Data de emissão não pode estar no futuro.");

        var now = DateTime.UtcNow;
        var processing = fromAsyncDocumentIngest
            ? ExpenseProcessingStatus.Received
            : ExpenseProcessingStatus.Completed;

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            Description = description.Trim(),
            Category = category.Trim(),
            Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant(),
            IssueDate = NormalizeToUtc(issueDate),
            DueDate = NormalizeToUtc(dueDate),
            ProcessingStatus = processing,
            ApprovalStatus = ExpenseApprovalStatus.PendingApproval,
            SettlementStatus = ExpenseSettlementStatus.Unpaid,
            RawDocumentId = rawDocumentId,
            ConfidenceScore = confidenceScore,
            LowConfidence = confidenceScore.HasValue && confidenceScore.Value < confidenceThreshold,
            CreatedAt = now,
            UpdatedAt = now,
            TotalAmount = 0m,
        };
        expense.RecomputeLegacyWorkflowStatus();
        return expense;
    }

    public ExpenseItem AddItem(string description, decimal quantity, decimal unitPrice)
    {
        EnsureMutable();
        if (!CanModifyLineItems())
            throw new DomainException(
                "Itens só podem ser alterados quando a despesa está pendente de aprovação, o processamento terminou e não está liquidada.");

        var item = ExpenseItem.Create(Id, description, quantity, unitPrice);
        _items.Add(item);
        Recalculate();
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureMutable();
        if (!CanModifyLineItems())
            throw new DomainException(
                "Itens só podem ser alterados quando a despesa está pendente de aprovação, o processamento terminou e não está liquidada.");

        var item = _items.FirstOrDefault(i => i.Id == itemId)
                   ?? throw new DomainException($"Item {itemId} não encontrado.");
        _items.Remove(item);
        Recalculate();
    }

    public void Approve()
    {
        EnsureMutable();
        if (ApprovalStatus != ExpenseApprovalStatus.PendingApproval)
            throw new DomainException("Apenas despesas em aprovação pendente podem ser aprovadas.");
        if (ProcessingStatus == ExpenseProcessingStatus.Failed)
            throw new DomainException(
                "Despesa com processamento falhado não pode ser aprovada. Rejeite, cancele ou reinicie a pipeline antes.");
        if (ProcessingStatus != ExpenseProcessingStatus.Completed
            && ProcessingStatus != ExpenseProcessingStatus.PartiallyCompleted)
            throw new DomainException("O processamento automático ainda não terminou; aguarde antes de aprovar.");
        if (_items.Count == 0)
            throw new DomainException("Não é possível aprovar despesa sem itens.");

        ApprovalStatus = ExpenseApprovalStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
        RecomputeLegacyWorkflowStatus();
    }

    public void Reject(string reason)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Motivo da rejeição é obrigatório.");
        if (ApprovalStatus != ExpenseApprovalStatus.PendingApproval)
            throw new DomainException("Apenas despesas pendentes de aprovação podem ser rejeitadas.");

        ApprovalStatus = ExpenseApprovalStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
        RecomputeLegacyWorkflowStatus();
    }

    public void Cancel(string reason)
    {
        EnsureMutable();
        if (SettlementStatus == ExpenseSettlementStatus.Paid)
            throw new DomainException("Despesas já totalmente liquidadas não podem ser canceladas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Motivo do cancelamento é obrigatório.");

        ApprovalStatus = ExpenseApprovalStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        RecomputeLegacyWorkflowStatus();
    }

    public Payment RegisterPayment(decimal amount, DateTime date, PaymentMethod method, string? referenceCode = null)
    {
        EnsureMutable();
        if (ApprovalStatus != ExpenseApprovalStatus.Approved)
            throw new DomainException("Apenas despesas aprovadas podem receber pagamentos.");
        if (amount <= 0m) throw new DomainException("Valor do pagamento deve ser > 0.");
        if (amount > OutstandingBalance + 0.001m)
            throw new DomainException($"Pagamento ({amount:0.00}) excede o saldo devedor ({OutstandingBalance:0.00}).");

        var payment = Payment.Create(TenantId, Id, amount, date, method, referenceCode);
        _payments.Add(payment);
        RefreshSettlementFromPayments();
        UpdatedAt = DateTime.UtcNow;
        return payment;
    }

    public void RefundPayment(Guid paymentId, string reason)
    {
        EnsureMutable();
        var payment = _payments.FirstOrDefault(p => p.Id == paymentId)
                      ?? throw new DomainException($"Pagamento {paymentId} não encontrado nesta despesa.");
        payment.Refund(reason);

        RefreshSettlementFromPayments();
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

    // --- Pipeline técnica (processamento) ---

    public void ApplyProcessingTransition(ExpenseProcessingStatus to, string? failureReason = null)
    {
        if (ProcessingStatus == to && to != ExpenseProcessingStatus.Failed)
            return;

        if (!ExpenseProcessingTransitionRules.IsAllowed(ProcessingStatus, to))
            throw new DomainException(ExpenseProcessingTransitionRules.DescribeDisallowed(ProcessingStatus, to));

        if (to == ExpenseProcessingStatus.Failed)
        {
            ProcessingFailureReason = failureReason;
            ProcessingFailedAt = DateTime.UtcNow;
        }
        else if (ProcessingStatus == ExpenseProcessingStatus.Failed && to == ExpenseProcessingStatus.Received)
        {
            // retry path: mantém contador; limpeza em RetryProcessingPipeline
        }
        else if (to != ExpenseProcessingStatus.Failed)
        {
            // Não limpar motivo em transições normais após falha — RetryProcessingPipeline limpa explicitamente.
        }

        ProcessingStatus = to;
        LastPipelineTransitionAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        RecomputeLegacyWorkflowStatus();
    }

    public void RetryProcessingPipeline()
    {
        EnsureMutable();
        if (ApprovalStatus == ExpenseApprovalStatus.Approved)
            throw new DomainException("Não é possível reiniciar a pipeline de uma despesa já aprovada.");
        if (ProcessingStatus != ExpenseProcessingStatus.Failed)
            throw new DomainException("Retry só é permitido quando o processamento está em Failed.");

        ProcessingRetryCount++;
        ProcessingFailureReason = null;
        ProcessingFailedAt = null;
        ApplyProcessingTransition(ExpenseProcessingStatus.Received);
    }

    public void MarkPersisting() => ApplyProcessingTransition(ExpenseProcessingStatus.Persisting);

    public void MarkProcessingCompleted() => ApplyProcessingTransition(ExpenseProcessingStatus.Completed);

    public void MarkProcessingFailed(string reason) =>
        ApplyProcessingTransition(ExpenseProcessingStatus.Failed, string.IsNullOrWhiteSpace(reason) ? "unknown" : reason.Trim());

    public void BeginPriceBenchmark()
    {
        EnsureMutable();
        if (ProcessingStatus == ExpenseProcessingStatus.Benchmarking)
            return;
        if (ProcessingStatus != ExpenseProcessingStatus.Completed
            && ProcessingStatus != ExpenseProcessingStatus.PartiallyCompleted)
            return;

        ApplyProcessingTransition(ExpenseProcessingStatus.Benchmarking);
    }

    public void CompletePriceBenchmark(bool cataloguePersisted)
    {
        EnsureMutable();
        if (ProcessingStatus != ExpenseProcessingStatus.Benchmarking)
            return;

        var target = cataloguePersisted
            ? ExpenseProcessingStatus.Completed
            : ExpenseProcessingStatus.PartiallyCompleted;
        ApplyProcessingTransition(target);
    }

    private bool CanModifyLineItems() =>
        ApprovalStatus == ExpenseApprovalStatus.PendingApproval
        && SettlementStatus != ExpenseSettlementStatus.Paid
        && ProcessingStatus != ExpenseProcessingStatus.Failed
        && ProcessingStatus != ExpenseProcessingStatus.Benchmarking;

    private void Recalculate()
    {
        TotalAmount = _items.Sum(i => i.TotalPrice);
        RefreshSettlementFromPayments();
        UpdatedAt = DateTime.UtcNow;
    }

    private void RefreshSettlementFromPayments()
    {
        var paid = TotalPaid;
        if (paid <= 0.001m)
            SettlementStatus = ExpenseSettlementStatus.Unpaid;
        else if (OutstandingBalance > 0.001m)
            SettlementStatus = ExpenseSettlementStatus.PartiallyPaid;
        else
            SettlementStatus = ExpenseSettlementStatus.Paid;

        RecomputeLegacyWorkflowStatus();
    }

    /// <summary>
    /// Mantém o campo <see cref="Status"/> alinhado com cancelamento, rejeição, liquidação, aprovação e falha técnica da pipeline.
    /// </summary>
    private void RecomputeLegacyWorkflowStatus()
    {
        if (ApprovalStatus == ExpenseApprovalStatus.Cancelled)
        {
            Status = ExpenseStatus.Cancelled;
            return;
        }

        if (ApprovalStatus == ExpenseApprovalStatus.Rejected)
        {
            Status = ExpenseStatus.Rejected;
            return;
        }

        if (SettlementStatus == ExpenseSettlementStatus.Paid)
        {
            Status = ExpenseStatus.Paid;
            return;
        }

        if (ApprovalStatus == ExpenseApprovalStatus.Approved)
        {
            Status = ExpenseStatus.Approved;
            return;
        }

        if (ProcessingStatus == ExpenseProcessingStatus.Failed)
        {
            Status = ExpenseStatus.ProcessingFailed;
            return;
        }

        // PendingApproval + pipeline em curso ou pronta para fila humana (filtros usam colunas explícitas).
        Status = ExpenseStatus.Pending;
    }

    private void EnsureMutable()
    {
        if (DeletedAt.HasValue)
            throw new DomainException("Despesa excluída não pode ser modificada.");
    }

    /// <summary>Datas de documento (emissão/vencimento) chegam como Unspecified ou Local; PostgreSQL timestamptz exige UTC.</summary>
    private static DateTime NormalizeToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static DateTime? NormalizeToUtc(DateTime? value) =>
        value.HasValue ? NormalizeToUtc(value.Value) : null;
}