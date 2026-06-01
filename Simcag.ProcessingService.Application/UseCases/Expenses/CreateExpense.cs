using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.Shared.Events;
using Simcag.Shared.Finance;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>
/// Cria uma despesa manual via UI/API com validação completa.
/// </summary>
public sealed record CreateExpenseCommand(
    Guid TenantId,
    Guid SupplierId,
    string Description,
    string Category,
    DateTime IssueDate,
    DateTime? DueDate,
    string Currency = "BRL",
    Guid? RawDocumentId = null,
    decimal? Amount = null) : IRequest<Guid>;

public sealed record CreateExpenseResult(Guid ExpenseId);

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId obrigatório.");
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("SupplierId obrigatório.");
        RuleFor(x => x.Description).NotEmpty().MinimumLength(1).WithMessage("Descrição obrigatória.");
        RuleFor(x => x.Category).NotEmpty().MinimumLength(1).WithMessage("Categoria obrigatória.");
        RuleFor(x => x.IssueDate).GreaterThan(DateTime.UtcNow.AddDays(-1)).WithMessage("Data de emissão não pode estar antes do dia anterior ao atual.");
    }
}

public sealed class CreateExpenseHandler : IRequestHandler<CreateExpenseCommand, Guid>
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CreateExpenseHandler> _log;

    public CreateExpenseHandler(
        IExpenseRepository expenses,
        ISupplierRepository suppliers,
        ITenantContext tenant,
        ILogger<CreateExpenseHandler> log)
    {
        _expenses = expenses;
        _suppliers = suppliers;
        _tenant = tenant;
        _log = log;
    }

    public async Task<Guid> Handle(CreateExpenseCommand request, CancellationToken ct)
    {
        if (!_tenant.HasTenant)
            throw new InvalidOperationException("TenantId ausente no scope.");

        // Converter IssueDate para UTC antes de salvar
        var issueDate = DateTime.SpecifyKind(request.IssueDate, DateTimeKind.Utc);
        
        // Converter DueDate para UTC se presente
        var dueDate = request.DueDate.HasValue 
            ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc) 
            : (DateTime?)null;

        var expense = Expense.Create(
            tenantId: request.TenantId,
            supplierId: Guid.NewGuid(), // Placeholder - deve ser providido pelo caller
            description: request.Description,
            category: request.Category,
            issueDate: issueDate,
            dueDate: dueDate,
            currency: string.IsNullOrWhiteSpace(request.Currency) ? "BRL" : request.Currency,
            rawDocumentId: null);

        expense.MarkPersisting();
        await _expenses.AddAsync(expense, ct);
        await _expenses.SaveChangesAsync(ct);

        _log.LogInformation(
            "Expense {ExpenseId} criada manualmente (desc: {Description}, cat: {Category}).",
            expense.Id,
            request.Description,
            request.Category);

        return expense.Id;
    }
}
