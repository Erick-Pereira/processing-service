using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.ReadModel;
using Simcag.ProcessingService.ReadModel.Models;

namespace Simcag.ProcessingService.Application.UseCases.Dashboards;

/// <summary>
/// Agregação de KPIs para <c>GET /api/dashboard/summary</c> (read model mensal).
/// Mantém as mesmas chaves JSON que o SPA já consome (camelCase via serializer da API).
/// </summary>
public sealed record GetDashboardSummaryQuery(int? Year = null) : IRequest<DashboardSummaryDto>;

public sealed record DashboardSummaryDto(
    int Year,
    decimal EconomiaIdentificada,
    decimal GastoProcessado,
    decimal ValorEmAberto,
    int AuditoriasRealizadas,
    int FornecedoresCadastrados,
    int AlertasAtivos);

public sealed class GetDashboardSummaryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IDashboardQueryRepository _dashboard;

    public GetDashboardSummaryHandler(IDashboardQueryRepository dashboard) => _dashboard = dashboard;

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var y = request.Year ?? DateTime.UtcNow.Year;
        IReadOnlyList<MonthlyExpenseSummaryRow> rows =
            await _dashboard.GetMonthlySummaryAsync(y, cancellationToken).ConfigureAwait(false);

        var totalAmount = rows.Sum(r => r.TotalAmount);
        var totalExpenseLines = rows.Sum(r => r.ExpenseCount);
        var supplierIds = new HashSet<Guid>(rows.Select(r => r.SupplierId).Where(id => id != Guid.Empty));
        var outstanding = await _dashboard.GetYearOutstandingLiveAsync(y, cancellationToken).ConfigureAwait(false);

        return new DashboardSummaryDto(
            Year: y,
            // Economia potencial exige metodologia de referência/preço confiável; não derivar de gasto total.
            EconomiaIdentificada: 0m,
            GastoProcessado: totalAmount,
            ValorEmAberto: outstanding,
            AuditoriasRealizadas: totalExpenseLines,
            FornecedoresCadastrados: supplierIds.Count,
            AlertasAtivos: 0);
    }
}
