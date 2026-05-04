using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

public sealed record ApproveExpenseCommand(Guid Id) : IRequest;

public sealed class ApproveExpenseValidator : AbstractValidator<ApproveExpenseCommand>
{
    public ApproveExpenseValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ApproveExpenseHandler : IRequestHandler<ApproveExpenseCommand>
{
    private readonly IExpenseRepository _expenses;
    public ApproveExpenseHandler(IExpenseRepository expenses) => _expenses = expenses;

    public async Task Handle(ApproveExpenseCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.Id, ct)
            ?? throw new NotFoundException("Expense", request.Id);
        expense.Approve();
        await _expenses.SaveChangesAsync(ct);
    }
}

public sealed record CancelExpenseCommand(Guid Id, string Reason) : IRequest;

public sealed class CancelExpenseValidator : AbstractValidator<CancelExpenseCommand>
{
    public CancelExpenseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class CancelExpenseHandler : IRequestHandler<CancelExpenseCommand>
{
    private readonly IExpenseRepository _expenses;
    public CancelExpenseHandler(IExpenseRepository expenses) => _expenses = expenses;

    public async Task Handle(CancelExpenseCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.Id, ct)
            ?? throw new NotFoundException("Expense", request.Id);
        expense.Cancel(request.Reason);
        await _expenses.SaveChangesAsync(ct);
    }
}
