using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.ReadModel;
using Simcag.ProcessingService.ReadModel.Models;

namespace Simcag.ProcessingService.Application.UseCases.Dashboards;

public sealed record GetMonthlyDashboardQuery(int Year) : IRequest<IReadOnlyList<MonthlyExpenseSummaryRow>>;

public sealed class GetMonthlyDashboardHandler : IRequestHandler<GetMonthlyDashboardQuery, IReadOnlyList<MonthlyExpenseSummaryRow>>
{
    private readonly IDashboardQueryRepository _repo;
    public GetMonthlyDashboardHandler(IDashboardQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<MonthlyExpenseSummaryRow>> Handle(GetMonthlyDashboardQuery q, CancellationToken ct) =>
        _repo.GetMonthlySummaryAsync(q.Year, ct);
}

public sealed record GetCategoryBreakdownQuery(DateTime From, DateTime To) : IRequest<IReadOnlyList<CategoryBreakdownRow>>;

public sealed class GetCategoryBreakdownHandler : IRequestHandler<GetCategoryBreakdownQuery, IReadOnlyList<CategoryBreakdownRow>>
{
    private readonly IDashboardQueryRepository _repo;
    public GetCategoryBreakdownHandler(IDashboardQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<CategoryBreakdownRow>> Handle(GetCategoryBreakdownQuery q, CancellationToken ct) =>
        _repo.GetCategoryBreakdownAsync(q.From, q.To, ct);
}

public sealed record GetSupplierRankingQuery(DateTime From, DateTime To, int Top) : IRequest<IReadOnlyList<SupplierRankingRow>>;

public sealed class GetSupplierRankingHandler : IRequestHandler<GetSupplierRankingQuery, IReadOnlyList<SupplierRankingRow>>
{
    private readonly IDashboardQueryRepository _repo;
    public GetSupplierRankingHandler(IDashboardQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<SupplierRankingRow>> Handle(GetSupplierRankingQuery q, CancellationToken ct) =>
        _repo.GetSupplierRankingAsync(q.From, q.To, q.Top, ct);
}

public sealed record GetCashFlowQuery(DateTime From, DateTime To) : IRequest<IReadOnlyList<CashFlowRow>>;

public sealed class GetCashFlowHandler : IRequestHandler<GetCashFlowQuery, IReadOnlyList<CashFlowRow>>
{
    private readonly IDashboardQueryRepository _repo;
    public GetCashFlowHandler(IDashboardQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<CashFlowRow>> Handle(GetCashFlowQuery q, CancellationToken ct) =>
        _repo.GetCashFlowAsync(q.From, q.To, ct);
}

public sealed record GetYearOverYearQuery(int YearsBack) : IRequest<IReadOnlyList<YearOverYearRow>>;

public sealed class GetYearOverYearHandler : IRequestHandler<GetYearOverYearQuery, IReadOnlyList<YearOverYearRow>>
{
    private readonly IDashboardQueryRepository _repo;
    public GetYearOverYearHandler(IDashboardQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<YearOverYearRow>> Handle(GetYearOverYearQuery q, CancellationToken ct) =>
        _repo.GetYearOverYearAsync(q.YearsBack, ct);
}

public sealed record RefreshDashboardCommand : IRequest;

public sealed class RefreshDashboardHandler : IRequestHandler<RefreshDashboardCommand>
{
    private readonly IDashboardQueryRepository _repo;
    public RefreshDashboardHandler(IDashboardQueryRepository repo) => _repo = repo;
    public Task Handle(RefreshDashboardCommand request, CancellationToken ct) =>
        _repo.RefreshMonthlyExpenseSummaryAsync(ct);
}
