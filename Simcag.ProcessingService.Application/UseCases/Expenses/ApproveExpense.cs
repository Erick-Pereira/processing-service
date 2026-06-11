using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Dashboard;
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

public sealed record RejectExpenseCommand(Guid Id, string Reason) : IRequest;

public sealed class RejectExpenseValidator : AbstractValidator<RejectExpenseCommand>
{
    public RejectExpenseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RejectExpenseHandler : IRequestHandler<RejectExpenseCommand>
{
    private readonly IExpenseRepository _expenses;
    private readonly IDashboardReadModelRefresher _dashboardRefresh;

    public RejectExpenseHandler(IExpenseRepository expenses, IDashboardReadModelRefresher dashboardRefresh)
    {
        _expenses = expenses;
        _dashboardRefresh = dashboardRefresh;
    }

    public async Task Handle(RejectExpenseCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.Id, ct)
            ?? throw new NotFoundException("Expense", request.Id);
        expense.Reject(request.Reason);
        await _expenses.SaveChangesAsync(ct);
        await _dashboardRefresh.RefreshAfterExpenseMutationAsync(ct);
    }
}

public sealed record RetryExpenseProcessingCommand(Guid Id) : IRequest;

public sealed class RetryExpenseProcessingValidator : AbstractValidator<RetryExpenseProcessingCommand>
{
    public RetryExpenseProcessingValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class RetryExpenseProcessingHandler : IRequestHandler<RetryExpenseProcessingCommand>
{
    private readonly IExpenseRepository _expenses;
    public RetryExpenseProcessingHandler(IExpenseRepository expenses) => _expenses = expenses;

    public async Task Handle(RetryExpenseProcessingCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.Id, ct)
            ?? throw new NotFoundException("Expense", request.Id);
        expense.RetryProcessingPipeline();
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
    private readonly IDashboardReadModelRefresher _dashboardRefresh;

    public CancelExpenseHandler(IExpenseRepository expenses, IDashboardReadModelRefresher dashboardRefresh)
    {
        _expenses = expenses;
        _dashboardRefresh = dashboardRefresh;
    }

    public async Task Handle(CancelExpenseCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.Id, ct)
            ?? throw new NotFoundException("Expense", request.Id);
        expense.Cancel(request.Reason);
        await _expenses.SaveChangesAsync(ct);
        await _dashboardRefresh.RefreshAfterExpenseMutationAsync(ct);
    }
}
