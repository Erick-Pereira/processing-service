using FluentAssertions;
using Moq;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;
using Xunit;

namespace Simcag.ProcessingService.Tests.UseCases;

public sealed class ApproveExpenseHandlerTests
{
    [Fact]
    public async Task Handle_throws_NotFoundException_when_expense_does_not_exist()
    {
        var expenseId = Guid.NewGuid();
        var repository = new Mock<IExpenseRepository>();
        repository
            .Setup(r => r.GetByIdWithChildrenAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expense?)null);

        var handler = new ApproveExpenseHandler(repository.Object);

        var act = () => handler.Handle(new ApproveExpenseCommand(expenseId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .Where(ex => ex.Resource == "Expense" && ex.Identifier == expenseId.ToString());
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_approves_expense_when_processing_completed_and_has_items()
    {
        var expense = Expense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Manutenção elevador",
            "Manutenção",
            DateTime.UtcNow.AddDays(-1),
            dueDate: null);

        expense.AddItem("Serviço mensal", 1m, 250m);
        var expenseId = expense.Id;

        var repository = new Mock<IExpenseRepository>();
        repository
            .Setup(r => r.GetByIdWithChildrenAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);

        var handler = new ApproveExpenseHandler(repository.Object);

        await handler.Handle(new ApproveExpenseCommand(expenseId), CancellationToken.None);

        expense.ApprovalStatus.Should().Be(ExpenseApprovalStatus.Approved);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_propagates_DomainException_when_expense_cannot_be_approved()
    {
        var expense = Expense.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Despesa sem itens",
            "Serviços",
            DateTime.UtcNow.AddDays(-1),
            dueDate: null);
        var expenseId = expense.Id;

        var repository = new Mock<IExpenseRepository>();
        repository
            .Setup(r => r.GetByIdWithChildrenAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);

        var handler = new ApproveExpenseHandler(repository.Object);

        var act = () => handler.Handle(new ApproveExpenseCommand(expenseId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*sem itens*");
    }
}
