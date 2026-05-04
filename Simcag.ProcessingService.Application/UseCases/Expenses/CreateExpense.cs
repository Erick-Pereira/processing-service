using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

public sealed record ExpenseItemInput(string Description, decimal Quantity, decimal UnitPrice);

public sealed record CreateExpenseCommand(
    Guid SupplierId,
    string Description,
    string Category,
    DateTime IssueDate,
    DateTime? DueDate,
    string Currency,
    IReadOnlyList<ExpenseItemInput> Items,
    Guid? RawDocumentId,
    decimal? ConfidenceScore) : IRequest<Guid>;

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Pelo menos um item é obrigatório.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0m);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0m);
        });
    }
}

public sealed class CreateExpenseHandler : IRequestHandler<CreateExpenseCommand, Guid>
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;
    private readonly ITenantContext _tenant;

    public CreateExpenseHandler(IExpenseRepository expenses, ISupplierRepository suppliers, ITenantContext tenant)
    {
        _expenses = expenses;
        _suppliers = suppliers;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateExpenseCommand request, CancellationToken ct)
    {
        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, ct)
            ?? throw new NotFoundException("Supplier", request.SupplierId);

        var expense = Expense.Create(
            tenantId: _tenant.TenantId,
            supplierId: supplier.Id,
            description: request.Description,
            category: request.Category,
            issueDate: request.IssueDate,
            dueDate: request.DueDate,
            currency: string.IsNullOrWhiteSpace(request.Currency) ? "BRL" : request.Currency,
            rawDocumentId: request.RawDocumentId,
            confidenceScore: request.ConfidenceScore);

        foreach (var item in request.Items)
        {
            expense.AddItem(item.Description, item.Quantity, item.UnitPrice);
        }

        await _expenses.AddAsync(expense, ct);
        await _expenses.SaveChangesAsync(ct);
        return expense.Id;
    }
}
