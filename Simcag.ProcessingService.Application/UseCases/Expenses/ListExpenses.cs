using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

public sealed record ListExpensesQuery(
    ExpenseStatus? Status,
    string? Category,
    Guid? SupplierId,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<PagedResult<ExpenseListItemDto>>;

public sealed class ListExpensesHandler : IRequestHandler<ListExpensesQuery, PagedResult<ExpenseListItemDto>>
{
    private readonly IExpenseRepository _expenses;
    public ListExpensesHandler(IExpenseRepository expenses) => _expenses = expenses;

    public async Task<PagedResult<ExpenseListItemDto>> Handle(ListExpensesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var (items, total) = await _expenses.ListAsync(q.Status, q.Category, q.SupplierId, q.From, q.To,
            (page - 1) * size, size, includePayments: false, ct);

        return new PagedResult<ExpenseListItemDto>(
            items.Select(e => new ExpenseListItemDto
            {
                Id = e.Id,
                SupplierId = e.SupplierId,
                Description = e.Description,
                Category = e.Category,
                IssueDate = e.IssueDate,
                DueDate = e.DueDate,
                Status = e.Status.ToString(),
                Currency = e.Currency,
                TotalAmount = e.TotalAmount,
            }).ToList(),
            total, page, size);
    }
}

public sealed record GetExpenseByIdQuery(Guid Id) : IRequest<ExpenseDetailDto>;

public sealed class GetExpenseByIdHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDetailDto>
{
    private readonly IExpenseRepository _expenses;
    public GetExpenseByIdHandler(IExpenseRepository expenses) => _expenses = expenses;

    public async Task<ExpenseDetailDto> Handle(GetExpenseByIdQuery q, CancellationToken ct)
    {
        var e = await _expenses.GetByIdWithChildrenAsync(q.Id, ct)
            ?? throw new NotFoundException("Expense", q.Id);

        return new ExpenseDetailDto
        {
            Id = e.Id,
            SupplierId = e.SupplierId,
            Description = e.Description,
            Category = e.Category,
            IssueDate = e.IssueDate,
            DueDate = e.DueDate,
            Status = e.Status.ToString(),
            Currency = e.Currency,
            TotalAmount = e.TotalAmount,
            TotalPaid = e.TotalPaid,
            OutstandingBalance = e.OutstandingBalance,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            Items = e.Items.Select(i => new ExpenseItemDto
            {
                Id = i.Id,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
            }).ToList(),
            Payments = e.Payments.Select(p => new PaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Method = p.Method.ToString(),
                ReferenceCode = p.ReferenceCode,
                IsRefunded = p.IsRefunded,
                RefundedAt = p.RefundedAt,
            }).ToList(),
        };
    }
}
