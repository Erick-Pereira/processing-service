using System.Globalization;
using MediatR;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.ReadModel.Models;

namespace Simcag.ProcessingService.Application.UseCases.Dashboards;

/// <summary>Cálculo puro de insights (sem cache PostgreSQL).</summary>
public static class OperationalInsightsComputer
{
    private const string MvSource = "mv_monthly_expense_summary (processing read model)";
    private const string BenchmarkSource = "catálogo de produtos (benchmark price-analysis)";
    private const decimal CategoryShareWarningThreshold = 0.40m;
    private const decimal MonthOverMonthWarningThreshold = 0.20m;
    private const decimal MarketDeviationInsightThreshold = 20m;

    public static async Task<OperationalInsightsEnvelope> ComputeAsync(
        IMediator mediator,
        IProductRepository products,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow.Date;
        var items = new List<OperationalInsightDto>();

        var yearBackFrom = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddYears(-1);
        var yearBackTo = now;
        var cats12m = await mediator.Send(new GetCategoryBreakdownQuery(yearBackFrom, yearBackTo), ct);
        TryAddCategoryConcentration(cats12m, yearBackFrom, yearBackTo, items);

        var yoy = await mediator.Send(new GetYearOverYearQuery(2), ct);
        TryAddMonthOverMonthSpend(yoy, items);

        await TryAddMarketPriceDeviationsAsync(products, items, ct);

        return new OperationalInsightsEnvelope
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Items = items
        };
    }

    private static async Task TryAddMarketPriceDeviationsAsync(
        IProductRepository products,
        List<OperationalInsightDto> sink,
        CancellationToken ct)
    {
        var rows = await products.ListTopMarketDeviationsAsync(MarketDeviationInsightThreshold, take: 5, ct);
        foreach (var row in rows)
        {
            var slug = Slug(row.ProductName);
            var severity = row.MarketDeviationPercentage >= 50m ? "warning" : "info";
            sink.Add(new OperationalInsightDto
            {
                Id = $"insight-market-deviation-{slug}-{row.BenchmarkAt:yyyyMMdd}",
                Kind = "market-price-deviation",
                Title = "Desvio de preço face ao mercado",
                Summary =
                    $"«{row.ProductName}» foi registado a {row.LastPrice:C} vs benchmark de mercado {row.MarketBenchmarkPrice:C} (+{row.MarketDeviationPercentage:F1}%).",
                Severity = severity,
                Confidence = "high",
                PrimaryPeriod = new OperationalInsightPeriodDto
                {
                    FromInclusive = row.BenchmarkAt.Date,
                    ToInclusive = row.BenchmarkAt.Date
                },
                ComparePeriod = null,
                DataSources = new[] { BenchmarkSource },
                Criteria =
                    $"Emitido quando MarketDeviationPercentage ≥ {MarketDeviationInsightThreshold}% após análise de preço (price-analysis).",
                Evidence = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["productName"] = row.ProductName,
                    ["lastPriceBrl"] = row.LastPrice.ToString("F2", CultureInfo.InvariantCulture),
                    ["marketBenchmarkBrl"] = row.MarketBenchmarkPrice.ToString("F2", CultureInfo.InvariantCulture),
                    ["deviationPercent"] = row.MarketDeviationPercentage.ToString("F1", CultureInfo.InvariantCulture),
                    ["benchmarkAtUtc"] = row.BenchmarkAt.ToString("O", CultureInfo.InvariantCulture)
                }
            });
        }
    }

    private static void TryAddCategoryConcentration(
        IReadOnlyList<CategoryBreakdownRow> cats,
        DateTime from,
        DateTime to,
        List<OperationalInsightDto> sink)
    {
        if (cats.Count == 0)
            return;

        var total = cats.Sum(c => c.TotalAmount);
        if (total <= 0m)
            return;

        var top = cats.OrderByDescending(c => c.TotalAmount).First();
        var share = top.TotalAmount / total;
        if (share < CategoryShareWarningThreshold)
            return;

        var slug = Slug(top.Category);
        sink.Add(new OperationalInsightDto
        {
            Id = $"insight-category-share-{from:yyyyMMdd}-{to:yyyyMMdd}-{slug}",
            Kind = "category-spend-concentration",
            Title = "Concentração de gastos por categoria",
            Summary =
                $"Nos últimos 12 meses, «{top.Category}» representa cerca de {share:P1} do total agregado no read model.",
            Severity = share >= 0.55m ? "warning" : "info",
            Confidence = "medium",
            PrimaryPeriod = new OperationalInsightPeriodDto { FromInclusive = from, ToInclusive = to },
            ComparePeriod = null,
            DataSources = new[] { MvSource },
            Criteria =
                $"Emitido quando a categoria com maior TotalAmount em 12 meses representa ≥ {CategoryShareWarningThreshold:P0} do somatório de categorias no mesmo período.",
            Evidence = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["topCategory"] = top.Category,
                ["shareOfTotal"] = share.ToString("P2", CultureInfo.InvariantCulture),
                ["totalAmountBrl"] = total.ToString("F2", CultureInfo.InvariantCulture),
                ["topCategoryAmountBrl"] = top.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["expenseLinesInCategory"] = top.ExpenseCount.ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static void TryAddMonthOverMonthSpend(IReadOnlyList<YearOverYearRow> rows, List<OperationalInsightDto> sink)
    {
        var ordered = rows.OrderBy(r => r.Year).ThenBy(r => r.Month).ToList();
        if (ordered.Count < 2)
            return;

        var prev = ordered[^2];
        var last = ordered[^1];
        if (prev.TotalAmount <= 0m)
            return;

        var delta = (last.TotalAmount - prev.TotalAmount) / prev.TotalAmount;
        if (Math.Abs(delta) < MonthOverMonthWarningThreshold)
            return;

        var direction = delta >= 0 ? "aumento" : "redução";
        var fromPrev = new DateTime(prev.Year, prev.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toPrev = fromPrev.AddMonths(1).AddDays(-1);
        var fromLast = new DateTime(last.Year, last.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toLast = fromLast.AddMonths(1).AddDays(-1);

        var pt = CultureInfo.GetCultureInfo("pt-BR");
        sink.Add(new OperationalInsightDto
        {
            Id = $"insight-mom-spend-{prev.Year}{prev.Month:00}-{last.Year}{last.Month:00}",
            Kind = "month-over-month-total-spend",
            Title = "Variação forte no total mensal (série agregada)",
            Summary =
                $"Entre {pt.DateTimeFormat.GetMonthName(prev.Month)} {prev.Year} e {pt.DateTimeFormat.GetMonthName(last.Month)} {last.Year}, o total mensal agregado teve {direction} de aproximadamente {Math.Abs(delta):P1}.",
            Severity = Math.Abs(delta) >= 0.35m ? "warning" : "info",
            Confidence = "low",
            PrimaryPeriod = new OperationalInsightPeriodDto { FromInclusive = fromLast, ToInclusive = toLast },
            ComparePeriod = new OperationalInsightPeriodDto { FromInclusive = fromPrev, ToInclusive = toPrev },
            DataSources = new[] { MvSource },
            Criteria =
                $"Comparar os dois últimos meses com dados na série YoY; emitir se |Δ| ≥ {MonthOverMonthWarningThreshold:P0} sobre o mês anterior.",
            Evidence = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["previousYearMonth"] = $"{prev.Year}-{prev.Month:00}",
                ["previousTotalBrl"] = prev.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["latestYearMonth"] = $"{last.Year}-{last.Month:00}",
                ["latestTotalBrl"] = last.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["relativeChange"] = delta.ToString("P2", CultureInfo.InvariantCulture)
            }
        });
    }

    private static string Slug(string category)
    {
        var s = string.Concat(category.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return s.Length > 0 ? s[..Math.Min(s.Length, 48)] : "cat";
    }
}
