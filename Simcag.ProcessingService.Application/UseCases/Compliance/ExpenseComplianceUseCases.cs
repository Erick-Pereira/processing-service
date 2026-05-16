using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Simcag.ProcessingService.Application.Compliance;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Application.UseCases.Compliance;

public static class ExpenseCompliancePresentation
{
    public static int ComputeScore(IReadOnlyList<ExpenseComplianceFindingDto> findings)
    {
        var score = 100;
        foreach (var f in findings)
        {
            if (f.Status == "OUTSTANDING")
            {
                score -= f.Severity switch
                {
                    "CRITICAL" => 22,
                    "HIGH" => 14,
                    "MEDIUM" => 8,
                    _ => 4,
                };
            }
            else if (f.Status == "WAIVED")
            {
                score -= 3;
            }
        }

        return Math.Clamp(score, 0, 100);
    }

    public static ExpenseComplianceFindingDto ToDto(ExpenseComplianceFinding f, IReadOnlyList<ExpenseComplianceCommentDto> comments) =>
        new()
        {
            Id = f.Id,
            ExpenseId = f.ExpenseId,
            RuleCode = f.RuleCode,
            Title = f.Title,
            Description = f.Description,
            Severity = f.Severity,
            Status = f.Status,
            Origin = f.Origin,
            Confidence = f.Confidence,
            DetailJson = f.DetailJson,
            EvidenceDocumentIdsJson = f.EvidenceDocumentIdsJson,
            EvaluatedAtUtc = f.EvaluatedAtUtc,
            CreatedAtUtc = f.CreatedAtUtc,
            UpdatedAtUtc = f.UpdatedAtUtc,
            WaivedAtUtc = f.WaivedAtUtc,
            WaivedByUserId = f.WaivedByUserId,
            WaivedByUserName = f.WaivedByUserName,
            WaivedReason = f.WaivedReason,
            Comments = comments,
        };

    public static ExpenseComplianceCommentDto ToCommentDto(ExpenseComplianceComment c) =>
        new()
        {
            Id = c.Id,
            Body = c.Body,
            AuthorUserId = c.AuthorUserId,
            AuthorUserName = c.AuthorUserName,
            CreatedAtUtc = c.CreatedAtUtc,
        };
}

public sealed record GetExpenseComplianceSnapshotQuery(Guid ExpenseId) : IRequest<ExpenseComplianceSnapshotDto>;

public sealed class GetExpenseComplianceSnapshotHandler : IRequestHandler<GetExpenseComplianceSnapshotQuery, ExpenseComplianceSnapshotDto>
{
    private readonly IExpenseRepository _expenses;
    private readonly IExpenseComplianceRepository _compliance;
    private readonly IMediator _mediator;

    public GetExpenseComplianceSnapshotHandler(
        IExpenseRepository expenses,
        IExpenseComplianceRepository compliance,
        IMediator mediator)
    {
        _expenses = expenses;
        _compliance = compliance;
        _mediator = mediator;
    }

    public async Task<ExpenseComplianceSnapshotDto> Handle(GetExpenseComplianceSnapshotQuery q, CancellationToken ct)
    {
        _ = await _expenses.GetByIdAsync(q.ExpenseId, ct) ?? throw new NotFoundException("Expense", q.ExpenseId);
        var existing = await _compliance.ListByExpenseAsync(q.ExpenseId, ct).ConfigureAwait(false);
        if (existing.Count == 0)
        {
            await _mediator.Send(new ReevaluateExpenseComplianceCommand(q.ExpenseId), ct).ConfigureAwait(false);
        }

        return await BuildSnapshotAsync(_compliance, q.ExpenseId, ct).ConfigureAwait(false);
    }

    internal static async Task<ExpenseComplianceSnapshotDto> BuildSnapshotAsync(
        IExpenseComplianceRepository repo,
        Guid expenseId,
        CancellationToken ct)
    {
        var findings = await repo.ListByExpenseAsync(expenseId, ct).ConfigureAwait(false);
        var dtos = new List<ExpenseComplianceFindingDto>();
        foreach (var f in findings)
        {
            var comments = await repo.ListCommentsByFindingAsync(f.Id, ct).ConfigureAwait(false);
            var cd = comments.Select(ExpenseCompliancePresentation.ToCommentDto).ToList();
            dtos.Add(ExpenseCompliancePresentation.ToDto(f, cd));
        }

        var score = ExpenseCompliancePresentation.ComputeScore(dtos);
        return new ExpenseComplianceSnapshotDto
        {
            ExpenseId = expenseId,
            ComplianceScore = score,
            OutstandingCount = dtos.Count(x => x.Status == "OUTSTANDING"),
            ClearCount = dtos.Count(x => x.Status == "CLEAR"),
            WaivedCount = dtos.Count(x => x.Status == "WAIVED"),
            HighRiskOpenCount = dtos.Count(x =>
                x.Status == "OUTSTANDING" && (x.Severity == "HIGH" || x.Severity == "CRITICAL")),
            Findings = dtos,
        };
    }
}

public sealed record ReevaluateExpenseComplianceCommand(Guid ExpenseId) : IRequest<ExpenseComplianceSnapshotDto>;

public sealed class ReevaluateExpenseComplianceHandler : IRequestHandler<ReevaluateExpenseComplianceCommand, ExpenseComplianceSnapshotDto>
{
    private readonly IExpenseRepository _expenses;
    private readonly IExpenseComplianceRepository _compliance;
    private readonly IAuditLogRepository _auditLogs;

    public ReevaluateExpenseComplianceHandler(
        IExpenseRepository expenses,
        IExpenseComplianceRepository compliance,
        IAuditLogRepository auditLogs)
    {
        _expenses = expenses;
        _compliance = compliance;
        _auditLogs = auditLogs;
    }

    public async Task<ExpenseComplianceSnapshotDto> Handle(ReevaluateExpenseComplianceCommand q, CancellationToken ct)
    {
        var expense = await _expenses.GetByIdWithChildrenAsync(q.ExpenseId, ct)
            ?? throw new NotFoundException("Expense", q.ExpenseId);

        var (auditItems, _) = await _auditLogs.ListAsync(
            nameof(Expense),
            q.ExpenseId,
            null,
            null,
            null,
            skip: 0,
            take: 250,
            ct).ConfigureAwait(false);

        var auditsNewest = auditItems.OrderByDescending(a => a.CreatedAt).ToList();
        var candidates = ExpenseComplianceEvaluator.Evaluate(expense, auditsNewest);

        var tracked = await _compliance.ListTrackedForExpenseAsync(q.ExpenseId, ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        foreach (var c in candidates)
        {
            var ex = tracked.FirstOrDefault(x =>
                string.Equals(x.RuleCode, c.RuleCode, StringComparison.OrdinalIgnoreCase));
            if (ex is null)
            {
                _compliance.Add(
                    ExpenseComplianceFinding.Create(
                        expense.TenantId,
                        expense.Id,
                        c.RuleCode,
                        c.Title,
                        c.Description,
                        c.Severity,
                        c.Status,
                        c.Origin,
                        c.Confidence,
                        c.DetailJson,
                        now));
            }
            else
            {
                ex.ApplyEngineUpdate(
                    c.Title,
                    c.Description,
                    c.Severity,
                    c.Status,
                    c.Confidence,
                    c.DetailJson,
                    now);
            }
        }

        await _compliance.SaveChangesAsync(ct).ConfigureAwait(false);
        return await GetExpenseComplianceSnapshotHandler.BuildSnapshotAsync(_compliance, q.ExpenseId, ct).ConfigureAwait(false);
    }
}

public sealed record ListComplianceFindingsQuery(
    string? Status,
    string? Severity,
    Guid? ExpenseId,
    int Page,
    int PageSize) : IRequest<PagedResult<ExpenseComplianceFindingDto>>;

public sealed class ListComplianceFindingsHandler : IRequestHandler<ListComplianceFindingsQuery, PagedResult<ExpenseComplianceFindingDto>>
{
    private readonly IExpenseComplianceRepository _repo;

    public ListComplianceFindingsHandler(IExpenseComplianceRepository repo) => _repo = repo;

    public async Task<PagedResult<ExpenseComplianceFindingDto>> Handle(ListComplianceFindingsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var skip = (page - 1) * size;

        var (items, total) = await _repo.ListTenantFindingsAsync(q.Status, q.Severity, q.ExpenseId, skip, size, ct)
            .ConfigureAwait(false);

        var dtos = new List<ExpenseComplianceFindingDto>();
        foreach (var f in items)
        {
            var comments = await _repo.ListCommentsByFindingAsync(f.Id, ct).ConfigureAwait(false);
            dtos.Add(
                ExpenseCompliancePresentation.ToDto(
                    f,
                    comments.Select(ExpenseCompliancePresentation.ToCommentDto).ToList()));
        }

        return new PagedResult<ExpenseComplianceFindingDto>(dtos, total, page, size);
    }
}

public sealed record GetComplianceDashboardQuery : IRequest<ComplianceDashboardDto>;

public sealed class GetComplianceDashboardHandler : IRequestHandler<GetComplianceDashboardQuery, ComplianceDashboardDto>
{
    private readonly IExpenseComplianceRepository _repo;

    public GetComplianceDashboardHandler(IExpenseComplianceRepository repo) => _repo = repo;

    public async Task<ComplianceDashboardDto> Handle(GetComplianceDashboardQuery _, CancellationToken ct)
    {
        var (outstanding, clear, waived, highOpen) = await _repo.CountTenantAsync(ct).ConfigureAwait(false);
        var distinctOpen = await _repo.CountDistinctExpensesWithOpenFindingsAsync(ct).ConfigureAwait(false);

        var score = 100 - Math.Min(100, outstanding * 4 + highOpen * 10 + waived * 2);
        score = Math.Clamp(score, 0, 100);

        return new ComplianceDashboardDto
        {
            ComplianceScore = score,
            OutstandingFindings = outstanding,
            ClearFindings = clear,
            WaivedFindings = waived,
            HighRiskOpen = highOpen,
            DistinctExpensesWithOpen = distinctOpen,
        };
    }
}

public sealed record ListComplianceRulesQuery : IRequest<IReadOnlyList<ComplianceRuleDefinitionDto>>;

public sealed class ListComplianceRulesHandler : IRequestHandler<ListComplianceRulesQuery, IReadOnlyList<ComplianceRuleDefinitionDto>>
{
    public Task<IReadOnlyList<ComplianceRuleDefinitionDto>> Handle(ListComplianceRulesQuery _, CancellationToken ct) =>
        Task.FromResult(ComplianceRuleCatalog.All);
}

public sealed record WaiveExpenseComplianceFindingCommand(Guid ExpenseId, Guid FindingId, string Reason) : IRequest;

public sealed class WaiveExpenseComplianceFindingValidator : AbstractValidator<WaiveExpenseComplianceFindingCommand>
{
    public WaiveExpenseComplianceFindingValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class WaiveExpenseComplianceFindingHandler : IRequestHandler<WaiveExpenseComplianceFindingCommand>
{
    private readonly IExpenseComplianceRepository _compliance;
    private readonly IAuditLogRepository _auditLogs;
    private readonly ICurrentUserContext _user;

    public WaiveExpenseComplianceFindingHandler(
        IExpenseComplianceRepository compliance,
        IAuditLogRepository auditLogs,
        ICurrentUserContext user)
    {
        _compliance = compliance;
        _auditLogs = auditLogs;
        _user = user;
    }

    public async Task Handle(WaiveExpenseComplianceFindingCommand req, CancellationToken ct)
    {
        var f = await _compliance.GetFindingAsync(req.ExpenseId, req.FindingId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("ExpenseComplianceFinding", req.FindingId);

        if (f.Status == "WAIVED")
            return;

        f.Waive(_user.UserId, _user.UserName, req.Reason);
        await _compliance.SaveChangesAsync(ct).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(new
        {
            expenseId = req.ExpenseId,
            findingId = req.FindingId,
            ruleCode = f.RuleCode,
            reason = req.Reason,
        });
        await _auditLogs.AppendAsync(
                AuditLog.FromEntry(
                    f.TenantId,
                    nameof(ExpenseComplianceFinding),
                    f.Id,
                    "ComplianceWaived",
                    null,
                    payload,
                    _user.UserId,
                    _user.UserName,
                    DateTime.UtcNow),
                ct)
            .ConfigureAwait(false);
    }
}

public sealed record AddExpenseComplianceCommentCommand(Guid ExpenseId, Guid FindingId, string Body) : IRequest;

public sealed class AddExpenseComplianceCommentValidator : AbstractValidator<AddExpenseComplianceCommentCommand>
{
    public AddExpenseComplianceCommentValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class AddExpenseComplianceCommentHandler : IRequestHandler<AddExpenseComplianceCommentCommand>
{
    private readonly IExpenseComplianceRepository _compliance;
    private readonly ICurrentUserContext _user;

    public AddExpenseComplianceCommentHandler(IExpenseComplianceRepository compliance, ICurrentUserContext user)
    {
        _compliance = compliance;
        _user = user;
    }

    public async Task Handle(AddExpenseComplianceCommentCommand req, CancellationToken ct)
    {
        var f = await _compliance.GetFindingAsync(req.ExpenseId, req.FindingId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("ExpenseComplianceFinding", req.FindingId);

        var tenant = f.TenantId;
        var comment = ExpenseComplianceComment.Create(
            tenant,
            f.Id,
            f.ExpenseId,
            req.Body,
            _user.UserId,
            _user.UserName);
        _compliance.AddComment(comment);
        await _compliance.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

public sealed record SetExpenseComplianceEvidenceCommand(Guid ExpenseId, Guid FindingId, IReadOnlyList<Guid> DocumentIds)
    : IRequest;

public sealed class SetExpenseComplianceEvidenceValidator : AbstractValidator<SetExpenseComplianceEvidenceCommand>
{
    public SetExpenseComplianceEvidenceValidator()
    {
        RuleFor(x => x.ExpenseId).NotEmpty();
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.DocumentIds).NotNull();
    }
}

public sealed class SetExpenseComplianceEvidenceHandler : IRequestHandler<SetExpenseComplianceEvidenceCommand>
{
    private readonly IExpenseComplianceRepository _compliance;

    public SetExpenseComplianceEvidenceHandler(IExpenseComplianceRepository compliance) => _compliance = compliance;

    public async Task Handle(SetExpenseComplianceEvidenceCommand req, CancellationToken ct)
    {
        var f = await _compliance.GetFindingAsync(req.ExpenseId, req.FindingId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("ExpenseComplianceFinding", req.FindingId);

        var json = JsonSerializer.Serialize(req.DocumentIds);
        f.SetEvidenceDocumentIdsJson(json);
        await _compliance.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
