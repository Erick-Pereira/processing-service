using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

public sealed record ListExpensesQuery(
    ExpenseStatus? Status,
    ExpenseProcessingStatus? ProcessingStatus,
    ExpenseApprovalStatus? ApprovalStatus,
    string? Category,
    Guid? SupplierId,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<PagedResult<ExpenseListItemDto>>;

public sealed class ListExpensesHandler : IRequestHandler<ListExpensesQuery, PagedResult<ExpenseListItemDto>>
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;

    public ListExpensesHandler(IExpenseRepository expenses, ISupplierRepository suppliers)
    {
        _expenses = expenses;
        _suppliers = suppliers;
    }

    public async Task<PagedResult<ExpenseListItemDto>> Handle(ListExpensesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var (items, total) = await _expenses.ListAsync(
            q.Status,
            q.ProcessingStatus,
            q.ApprovalStatus,
            q.Category,
            q.SupplierId,
            q.From,
            q.To,
            (page - 1) * size,
            size,
            includePayments: false,
            ct);

        var supplierNames = await _suppliers.GetNamesByIdsAsync(items.Select(e => e.SupplierId), ct)
            .ConfigureAwait(false);

        return new PagedResult<ExpenseListItemDto>(
            items.Select(e => Map(e, supplierNames.GetValueOrDefault(e.SupplierId))).ToList(),
            total, page, size);
    }

    private static ExpenseListItemDto Map(Expense e, string? supplierName) => new()
    {
        Id = e.Id,
        SupplierId = e.SupplierId,
        SupplierName = supplierName,
        Description = e.Description,
        Category = e.Category,
        IssueDate = e.IssueDate,
        DueDate = e.DueDate,
        Status = e.Status.ToString(),
        ProcessingStatus = e.ProcessingStatus.ToString(),
        ApprovalStatus = e.ApprovalStatus.ToString(),
        SettlementStatus = e.SettlementStatus.ToString(),
        ProcessingFailureReason = e.ProcessingFailureReason,
        LastPipelineTransitionAt = e.LastPipelineTransitionAt,
        ConfidenceScore = e.ConfidenceScore,
        LowConfidence = e.LowConfidence,
        Currency = e.Currency,
        TotalAmount = e.TotalAmount,
    };
}

public sealed record GetExpenseByIdQuery(Guid Id) : IRequest<ExpenseDetailDto>;

public sealed class GetExpenseByIdHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDetailDto>
{
    private readonly IExpenseRepository _expenses;
    private readonly IAuditLogRepository _auditLogs;
    private readonly ISupplierRepository _suppliers;

    public GetExpenseByIdHandler(
        IExpenseRepository expenses,
        IAuditLogRepository auditLogs,
        ISupplierRepository suppliers)
    {
        _expenses = expenses;
        _auditLogs = auditLogs;
        _suppliers = suppliers;
    }

    public async Task<ExpenseDetailDto> Handle(GetExpenseByIdQuery q, CancellationToken ct)
    {
        var e = await _expenses.GetByIdWithChildrenAsync(q.Id, ct)
            ?? throw new NotFoundException("Expense", q.Id);

        var supplier = await _suppliers.GetByIdAsync(e.SupplierId, ct).ConfigureAwait(false);

        var (auditItems, _) = await _auditLogs.ListAsync(
            nameof(Expense),
            q.Id,
            null,
            null,
            null,
            skip: 0,
            take: 250,
            ct).ConfigureAwait(false);

        var auditsChrono = auditItems.OrderBy(a => a.CreatedAt).ToList();
        var timeline = ExpenseOperationalSnapshotBuilder.BuildTimeline(e, auditsChrono);
        var governance = ExpenseOperationalSnapshotBuilder.BuildGovernance(e);

        return new ExpenseDetailDto
        {
            Id = e.Id,
            SupplierId = e.SupplierId,
            SupplierName = supplier?.Name,
            Description = e.Description,
            Category = e.Category,
            IssueDate = e.IssueDate,
            DueDate = e.DueDate,
            Status = e.Status.ToString(),
            ProcessingStatus = e.ProcessingStatus.ToString(),
            ApprovalStatus = e.ApprovalStatus.ToString(),
            SettlementStatus = e.SettlementStatus.ToString(),
            ProcessingFailureReason = e.ProcessingFailureReason,
            ProcessingFailedAt = e.ProcessingFailedAt,
            ProcessingRetryCount = e.ProcessingRetryCount,
            LastPipelineTransitionAt = e.LastPipelineTransitionAt,
            Currency = e.Currency,
            TotalAmount = e.TotalAmount,
            TotalPaid = e.TotalPaid,
            OutstandingBalance = e.OutstandingBalance,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            RawDocumentId = e.RawDocumentId,
            ConfidenceScore = e.ConfidenceScore,
            LowConfidence = e.LowConfidence,
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
            OperationalTimeline = timeline,
            Governance = governance,
        };
    }
}
