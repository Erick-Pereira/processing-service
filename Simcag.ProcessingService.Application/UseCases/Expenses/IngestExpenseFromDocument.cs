using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>
/// Cria uma <see cref="Expense"/> em estado <c>Pending</c> a partir do payload do
/// <c>DataIngestedEvent</c> (ingestion → processing). Idempotente por <see cref="RawDocumentId"/>:
/// se já existir uma despesa para o mesmo documento, retorna o ID existente sem reprocessar.
///
/// Importante: o handler espera que o <c>ITenantContext</c> ambiente já esteja populado
/// (em consumers RabbitMQ, isso é feito pelo worker via <see cref="AmbientPrincipal"/>).
/// </summary>
public sealed record IngestExpenseFromDocumentCommand(
    Guid RawDocumentId,
    string DocumentType,
    string? Description,
    decimal? Amount,
    DateTime? IssueDate,
    string? SupplierName,
    string? SupplierTaxId,
    string? FallbackCategory = "Outros") : IRequest<IngestExpenseFromDocumentResult>;

public sealed record IngestExpenseFromDocumentResult(Guid ExpenseId, bool AlreadyIngested);

public sealed class IngestExpenseFromDocumentValidator : AbstractValidator<IngestExpenseFromDocumentCommand>
{
    public IngestExpenseFromDocumentValidator()
    {
        RuleFor(x => x.RawDocumentId).NotEmpty();
    }
}

public sealed class IngestExpenseFromDocumentHandler
    : IRequestHandler<IngestExpenseFromDocumentCommand, IngestExpenseFromDocumentResult>
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;
    private readonly ITenantContext _tenant;
    private readonly ILogger<IngestExpenseFromDocumentHandler> _log;

    public IngestExpenseFromDocumentHandler(
        IExpenseRepository expenses,
        ISupplierRepository suppliers,
        ITenantContext tenant,
        ILogger<IngestExpenseFromDocumentHandler> log)
    {
        _expenses = expenses;
        _suppliers = suppliers;
        _tenant = tenant;
        _log = log;
    }

    public async Task<IngestExpenseFromDocumentResult> Handle(
        IngestExpenseFromDocumentCommand request,
        CancellationToken ct)
    {
        if (!_tenant.HasTenant)
            throw new InvalidOperationException(
                "TenantId ausente no scope. Workers devem popular AmbientPrincipal antes de despachar este command.");

        var existing = await _expenses.GetByRawDocumentIdAsync(request.RawDocumentId, ct);
        if (existing is not null)
        {
            _log.LogInformation(
                "DataIngestedEvent {RawDocumentId} já gerou Expense {ExpenseId}; ignorando duplicata.",
                request.RawDocumentId, existing.Id);
            return new IngestExpenseFromDocumentResult(existing.Id, AlreadyIngested: true);
        }

        var supplier = await ResolveOrCreateSupplierAsync(request, ct);
        var (description, category) = NormalizeDescriptionAndCategory(request);
        var amount = request.Amount ?? 0m;
        var issueDate = request.IssueDate ?? DateTime.UtcNow.Date;

        var confidence = ComputeConfidence(request);

        var expense = Expense.Create(
            tenantId: _tenant.TenantId,
            supplierId: supplier.Id,
            description: description,
            category: category,
            issueDate: issueDate,
            dueDate: null,
            currency: "BRL",
            rawDocumentId: request.RawDocumentId,
            confidenceScore: confidence);

        // Sempre cria pelo menos 1 ExpenseItem (invariante: aprovação requer items).
        // Quando o valor é 0 (extração falhou), o item fica com unitPrice=0 e a despesa
        // entra no fluxo de revisão manual sinalizada por LowConfidence=true.
        expense.AddItem(description, quantity: 1m, unitPrice: amount);

        await _expenses.AddAsync(expense, ct);
        await _expenses.SaveChangesAsync(ct);

        _log.LogInformation(
            "Expense {ExpenseId} criada a partir do documento {RawDocumentId} (valor R$ {Amount:0.00}, confidence {Confidence:P0}).",
            expense.Id, request.RawDocumentId, amount, confidence);

        return new IngestExpenseFromDocumentResult(expense.Id, AlreadyIngested: false);
    }

    private async Task<Supplier> ResolveOrCreateSupplierAsync(
        IngestExpenseFromDocumentCommand req,
        CancellationToken ct)
    {
        var taxId = NormalizeTaxId(req.SupplierTaxId);
        var name = string.IsNullOrWhiteSpace(req.SupplierName) ? "Fornecedor não identificado" : req.SupplierName.Trim();

        // Caminho ideal: temos um documento válido extraído.
        if (taxId is not null)
        {
            return await _suppliers.UpsertByDocumentOrNameAsync(name, taxId, category: null, ct);
        }

        // Fallback: usa o tenant como discriminator. Para evitar colisão com o índice único
        // (tenant_id, document) reservamos um placeholder por tenant: "00000000000000".
        // Ele agrupa todas as despesas de fornecedores não identificados sob um único bucket
        // por tenant, o que é o comportamento correto até a revisão manual.
        return await _suppliers.UpsertByDocumentOrNameAsync(name, "00000000000000", category: null, ct);
    }

    private static (string description, string category) NormalizeDescriptionAndCategory(
        IngestExpenseFromDocumentCommand req)
    {
        var description = string.IsNullOrWhiteSpace(req.Description)
            ? $"{req.DocumentType} ingerido em {DateTime.UtcNow:yyyy-MM-dd}"
            : req.Description.Trim();

        var category = string.IsNullOrWhiteSpace(req.FallbackCategory) ? "Outros" : req.FallbackCategory!;
        return (description, category);
    }

    /// <summary>
    /// Heurística de confidence: cada campo extraído contribui com peso. Tudo extraído ⇒ ~1.0;
    /// nada extraído ⇒ 0. Usado para o flag <c>LowConfidence</c> do agregado e para priorizar
    /// revisão manual no dashboard.
    /// </summary>
    private static decimal ComputeConfidence(IngestExpenseFromDocumentCommand req)
    {
        var score = 0m;
        if (req.Amount.HasValue && req.Amount.Value > 0m) score += 0.40m;
        if (req.IssueDate.HasValue) score += 0.20m;
        if (!string.IsNullOrWhiteSpace(req.SupplierTaxId)) score += 0.20m;
        if (!string.IsNullOrWhiteSpace(req.SupplierName)) score += 0.10m;
        if (!string.IsNullOrWhiteSpace(req.Description)) score += 0.10m;
        return Math.Round(score, 3);
    }

    private static string? NormalizeTaxId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string([.. raw.Where(char.IsDigit)]);
        return digits.Length is 11 or 14 ? digits : null;
    }
}
