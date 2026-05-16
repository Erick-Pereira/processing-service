using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.Shared.Events;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>
/// Cria uma <see cref="Expense"/> com pipeline técnica explícita a partir do payload do
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
    string? FallbackCategory = "Outros",
    IReadOnlyList<IngestedExpenseLine>? Lines = null,
    string? RawText = null) : IRequest<IngestExpenseFromDocumentResult>;

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

        // Ingestão por vezes envia só o cabeçalho sintético ("BALANCE_SHEET — 9 itens") como única linha,
        // ou Lines perde-se na mensagem mas Description mantém-se — nestes casos Lines não está "vazio"
        // e o fallback RawText tinha de ser ignorado. Unifica com deteção desse agregado.
        IReadOnlyList<IngestedExpenseLine>? linesForIngest = request.Lines;
        var fromRaw = BalanceteRawTextLineExtractor.TryExtractLines(request.RawText, request.DocumentType);
        if (fromRaw is { Count: > 0 } extracted)
        {
            var n = linesForIngest?.Count ?? 0;
            var useExtracted =
                n == 0
                || (n == 1
                    && LooksLikeIngestionSyntheticMultiItemSummary(linesForIngest![0].Description, request.Description)
                    && extracted.Count >= 2);

            if (useExtracted)
            {
                _log.LogWarning(
                    "Expense ingest {DocumentId}: usando {ExtractedCount} linhas do RawText (evento tinha {PreviousCount} linha(s); balancete/relatório compacto).",
                    request.RawDocumentId, extracted.Count, n);
                linesForIngest = extracted;
            }
        }

        var supplier = await ResolveOrCreateSupplierAsync(request, ct);
        var structuredLines = NormalizeStructuredLines(linesForIngest);
        var (description, category) = NormalizeDescriptionAndCategory(request);
        (description, category) = RefineAggregateFromStructuredLines(
            description,
            category,
            structuredLines,
            request.DocumentType);

        var issueDate = request.IssueDate ?? DateTime.UtcNow.Date;

        var confidence = ComputeConfidence(request, linesForIngest);

        var expense = Expense.Create(
            tenantId: _tenant.TenantId,
            supplierId: supplier.Id,
            description: description,
            category: category,
            issueDate: issueDate,
            dueDate: null,
            currency: "BRL",
            rawDocumentId: request.RawDocumentId,
            confidenceScore: confidence,
            fromAsyncDocumentIngest: true);

        expense.ApplyProcessingTransition(ExpenseProcessingStatus.Enriching);

        var fallbackAmount = request.Amount ?? 0m;

        // Sempre cria pelo menos 1 ExpenseItem (invariante: aprovação requer items).
        if (structuredLines.Count > 0)
        {
            var sumLines = structuredLines.Sum(l => l.UnitPrice);
            if (sumLines <= 0m && fallbackAmount > 0m)
            {
                expense.AddItem(description, quantity: 1m, unitPrice: fallbackAmount);
            }
            else
            {
                foreach (var line in structuredLines)
                    expense.AddItem(line.Description, quantity: 1m, unitPrice: line.UnitPrice);
            }
        }
        else
        {
            expense.AddItem(description, quantity: 1m, unitPrice: fallbackAmount);
        }

        var amount = expense.TotalAmount;

        expense.MarkPersisting();
        await _expenses.AddAsync(expense, ct);
        await _expenses.SaveChangesAsync(ct);
        expense.MarkProcessingCompleted();
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
        if (description.Length > 500)
            description = description[..500];

        var category = string.IsNullOrWhiteSpace(req.FallbackCategory) ? "Outros" : req.FallbackCategory!;
        return (description, category);
    }

    private static (string description, string category) RefineAggregateFromStructuredLines(
        string description,
        string category,
        List<(string Description, decimal UnitPrice)> lines,
        string documentType)
    {
        if (lines.Count == 0)
            return (description, category);

        if (!IsSyntheticBalanceSheetHeader(description))
            return (description, category);

        var title = BuildHumanReadableExpenseTitle(lines, documentType);
        var cat = InferExpenseCategoryFromLines(lines, documentType, category);
        return (title, cat);
    }

    private static bool IsSyntheticBalanceSheetHeader(string description)
    {
        var d = description.Trim();
        if (d.Length == 0)
            return false;
        if (d.Contains("BALANCE_SHEET", StringComparison.OrdinalIgnoreCase))
            return true;
        return SyntheticMultiItemSummaryRx.IsMatch(d);
    }

    private static string BuildHumanReadableExpenseTitle(
        List<(string Description, decimal UnitPrice)> lines,
        string documentType)
    {
        var docLabel = documentType.Contains("BALANCE", StringComparison.OrdinalIgnoreCase)
            ? "Balancete de despesas"
            : "Despesas";
        var n = lines.Count;
        var samples = string.Join(" · ", lines.Take(2).Select(l => l.Description));
        var more = n > 2 ? $" (+{n - 2})" : "";
        var s = $"{docLabel} ({n} itens): {samples}{more}";
        return s.Length > 500 ? s[..500] : s;
    }

    private const int MaxExpenseCategoryLength = 120;

    private static string InferExpenseCategoryFromLines(
        List<(string Description, decimal UnitPrice)> lines,
        string documentType,
        string fallback)
    {
        if (documentType.Contains("BALANCE", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = TryExtractLineCategoryPrefix(lines[0].Description);
            if (!string.IsNullOrWhiteSpace(prefix))
                return TruncateCategory(prefix);
        }

        var p = TryExtractLineCategoryPrefix(lines[0].Description);
        return string.IsNullOrWhiteSpace(p) ? fallback : TruncateCategory(p);
    }

    private static string? TryExtractLineCategoryPrefix(string lineDescription)
    {
        if (string.IsNullOrWhiteSpace(lineDescription))
            return null;
        var s = lineDescription.Trim();
        var sep = s.IndexOf('—');
        if (sep < 0)
            sep = s.IndexOf(" - ");
        if (sep <= 0)
            return null;
        return s[..sep].Trim();
    }

    private static string TruncateCategory(string c) =>
        c.Length <= MaxExpenseCategoryLength ? c : c[..MaxExpenseCategoryLength];

    /// <summary>
    /// Heurística de confidence: cada campo extraído contribui com peso. Tudo extraído ⇒ ~1.0;
    /// nada extraído ⇒ 0. Usado para o flag <c>LowConfidence</c> do agregado e para priorizar
    /// revisão manual no dashboard.
    /// </summary>
    private static decimal ComputeConfidence(IngestExpenseFromDocumentCommand req, IReadOnlyList<IngestedExpenseLine>? linesOverride = null)
    {
        var score = 0m;
        var lines = (linesOverride ?? req.Lines)?.Where(l => !string.IsNullOrWhiteSpace(l.Description)).ToList();
        if (lines is { Count: > 0 })
        {
            score += 0.25m;
            if (lines.Any(l => l.Amount > 0m)) score += 0.35m;
        }
        else if (req.Amount.HasValue && req.Amount.Value > 0m)
        {
            score += 0.40m;
        }

        if (req.IssueDate.HasValue) score += 0.15m;
        if (!string.IsNullOrWhiteSpace(req.SupplierTaxId)) score += 0.15m;
        if (!string.IsNullOrWhiteSpace(req.SupplierName)) score += 0.08m;
        if (!string.IsNullOrWhiteSpace(req.Description)) score += 0.07m;
        return Math.Round(Math.Min(score, 1m), 3);
    }

    private const int MaxItemDescriptionLength = 500;

    private static List<(string Description, decimal UnitPrice)> NormalizeStructuredLines(
        IReadOnlyList<IngestedExpenseLine>? lines)
    {
        if (lines is null || lines.Count == 0)
            return [];

        var list = new List<(string Description, decimal UnitPrice)>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description))
                continue;
            var desc = line.Description.Trim();
            if (desc.Length > MaxItemDescriptionLength)
                desc = desc[..MaxItemDescriptionLength];
            var price = line.Amount < 0m ? 0m : line.Amount;
            list.Add((desc, price));
        }

        return list;
    }

    private static string? NormalizeTaxId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string([.. raw.Where(char.IsDigit)]);
        return digits.Length is 11 or 14 ? digits : null;
    }

    /// <summary>
    /// Cabeçalho gerado em PublishRawEventUseCase quando há várias linhas com valor:
    /// <c>{tipo} — N itens</c>. Se só isto chega como única linha, há de se reextrair do RawText.
    /// </summary>
    private static readonly Regex SyntheticMultiItemSummaryRx = new(
        @"[\u2014\u2013\-]\s*\d+\s+itens\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool LooksLikeIngestionSyntheticMultiItemSummary(string? lineDescription, string? headerDescription)
    {
        if (!string.IsNullOrWhiteSpace(lineDescription)
            && SyntheticMultiItemSummaryRx.IsMatch(lineDescription.Trim()))
            return true;

        var h = headerDescription?.Trim();
        var l = lineDescription?.Trim();
        if (string.IsNullOrEmpty(h) || string.IsNullOrEmpty(l))
            return false;

        if (!string.Equals(l, h, StringComparison.OrdinalIgnoreCase))
            return false;

        return SyntheticMultiItemSummaryRx.IsMatch(h);
    }
}
