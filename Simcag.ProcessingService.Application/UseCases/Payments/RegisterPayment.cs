using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Application.UseCases.Payments;

public sealed record RegisterPaymentCommand(
    Guid ExpenseId,
    decimal Amount,
    DateTime PaymentDate,
    PaymentMethod Method,
    string? ReferenceCode) : IRequest<Guid>;

public sealed class RegisterPaymentValidator : AbstractValidator<RegisterPaymentCommand>
{
    public RegisterPaymentValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

public sealed class RegisterPaymentHandler : IRequestHandler<RegisterPaymentCommand, Guid>
{
    private readonly IExpenseRepository _expenses;
    private readonly IPaymentRepository _payments;
    public RegisterPaymentHandler(IExpenseRepository expenses, IPaymentRepository payments)
    {
        _expenses = expenses;
        _payments = payments;
    }

    public async Task<Guid> Handle(RegisterPaymentCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.ExpenseId, ct)
            ?? throw new NotFoundException("Expense", request.ExpenseId);
        var payment = expense.RegisterPayment(request.Amount, request.PaymentDate, request.Method, request.ReferenceCode);
        await _payments.AddAsync(payment, ct);
        await _expenses.SaveChangesAsync(ct);
        return payment.Id;
    }
}

public sealed record RefundPaymentCommand(Guid ExpenseId, Guid PaymentId, string Reason) : IRequest;

public sealed class RefundPaymentValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class RefundPaymentHandler : IRequestHandler<RefundPaymentCommand>
{
    private readonly IExpenseRepository _expenses;
    public RefundPaymentHandler(IExpenseRepository expenses) => _expenses = expenses;

    public async Task Handle(RefundPaymentCommand request, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(request.ExpenseId, ct)
            ?? throw new NotFoundException("Expense", request.ExpenseId);
        expense.RefundPayment(request.PaymentId, request.Reason);
        await _expenses.SaveChangesAsync(ct);
    }
}
