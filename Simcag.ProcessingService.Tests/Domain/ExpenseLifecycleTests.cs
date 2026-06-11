using FluentAssertions;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;
using Xunit;

namespace Simcag.ProcessingService.Tests.Domain;

public sealed class ExpenseLifecycleTests
{
    [Fact]
    public void Cancel_throws_when_active_payments_exist()
    {
        var expense = CreateApprovedExpenseWithItem();
        expense.RegisterPayment(50m, DateTime.UtcNow, PaymentMethod.Pix);

        var act = () => expense.Cancel("motivo");

        act.Should().Throw<DomainException>()
            .WithMessage("*pagamentos ativos*");
    }

    [Fact]
    public void Cancel_succeeds_when_no_active_payments()
    {
        var expense = CreateManualExpenseWithItem();

        expense.Cancel("duplicidade");

        expense.ApprovalStatus.Should().Be(ExpenseApprovalStatus.Cancelled);
        expense.OutstandingBalance.Should().Be(0m);
    }

    [Fact]
    public void Reject_zeroes_outstanding_balance()
    {
        var expense = CreateManualExpenseWithItem();

        expense.Reject("nota inválida");

        expense.ApprovalStatus.Should().Be(ExpenseApprovalStatus.Rejected);
        expense.OutstandingBalance.Should().Be(0m);
    }

    [Fact]
    public void ApplyProcessingTransition_throws_when_expense_cancelled()
    {
        var expense = CreateManualExpenseWithItem();
        expense.Cancel("teste");

        var act = () => expense.ApplyProcessingTransition(ExpenseProcessingStatus.Enriching);

        act.Should().Throw<DomainException>()
            .WithMessage("*encerrada*");
    }

    private static Expense CreateManualExpenseWithItem()
    {
        var expense = Expense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Despesa teste",
            "Serviços",
            DateTime.UtcNow.AddDays(-1),
            dueDate: null,
            fromAsyncDocumentIngest: false);
        expense.AddItem("Item", 1m, 100m);
        return expense;
    }

    private static Expense CreateApprovedExpenseWithItem()
    {
        var expense = CreateManualExpenseWithItem();
        expense.Approve();
        return expense;
    }
}
