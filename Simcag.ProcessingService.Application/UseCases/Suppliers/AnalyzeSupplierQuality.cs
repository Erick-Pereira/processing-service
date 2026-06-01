using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.ReadModel;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

public sealed record AnalyzeSupplierQualityQuery : IRequest<SupplierQualityAnalysisDto>;

public sealed class AnalyzeSupplierQualityHandler : IRequestHandler<AnalyzeSupplierQualityQuery, SupplierQualityAnalysisDto>
{
    private readonly ISupplierQualityReadModel _readModel;

    public AnalyzeSupplierQualityHandler(ISupplierQualityReadModel readModel) => _readModel = readModel;

    public async Task<SupplierQualityAnalysisDto> Handle(AnalyzeSupplierQualityQuery request, CancellationToken ct)
    {
        var expenseStats = await _readModel.GetExpenseStatsAsync(ct).ConfigureAwait(false);
        var complianceStats = await _readModel.GetComplianceStatsAsync(ct).ConfigureAwait(false);
        var priceAudits = await _readModel.GetPriceAuditPayloadsAsync(ct).ConfigureAwait(false);

        var complianceBySupplier = complianceStats.ToDictionary(x => x.SupplierId);
        var priceBySupplier = SupplierQualityScorer.AggregatePriceAudits(priceAudits);

        var items = expenseStats
            .Select(s => SupplierQualityScorer.Score(
                s,
                priceBySupplier.GetValueOrDefault(s.SupplierId),
                complianceBySupplier.GetValueOrDefault(s.SupplierId)))
            .OrderByDescending(x => x.Score ?? -1m)
            .ThenByDescending(x => x.ExpenseCount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = new SupplierQualitySummaryDto
        {
            TotalSuppliers = items.Count,
            RecommendedCount = items.Count(x => x.Tier == "RECOMENDADO"),
            AcceptableCount = items.Count(x => x.Tier == "ACEITAVEL"),
            AttentionCount = items.Count(x => x.Tier == "ATENCAO"),
            HighRiskCount = items.Count(x => x.Tier == "RISCO"),
            InsufficientDataCount = items.Count(x => x.Tier == "DADOS_INSUFICIENTES"),
        };

        return new SupplierQualityAnalysisDto
        {
            Suppliers = items,
            Summary = summary,
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }
}
