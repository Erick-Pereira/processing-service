using Simcag.ProcessingService.Application.UseCases.Products;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.Events;
using Simcag.Shared.Finance;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>
/// Reconcilia preço/qtd do pipeline de benchmark com os itens persistidos da NF.
/// </summary>
public static class ExpenseItemPriceReconciler
{
    public sealed record Result(
        decimal NfUnitPrice,
        int NfQuantity,
        decimal NfLineTotal,
        decimal CorrectedDeviationPercentage,
        bool PriceAuditCorrected);

    public static Result? TryReconcile(PriceAnalyzedEvent evt, IReadOnlyCollection<ExpenseItem> items)
    {
        if (items.Count == 0)
            return null;

        var match = FindMatchingItem(evt.ProductName, items);
        if (match is null)
            return null;

        var nfUnit = match.UnitPrice;
        var nfQty = (int)Math.Round(match.Quantity, MidpointRounding.AwayFromZero);
        var nfTotal = match.TotalPrice;

        if (nfUnit <= 0 || nfQty <= 0)
            return null;

        var auditUnit = evt.LastPrice;
        var unitDiverges = auditUnit > 0 && Math.Abs(auditUnit - nfUnit) / nfUnit > 0.05m;
        var qtyDiverges = evt.Quantity.HasValue && evt.Quantity.Value != nfQty;

        if (!unitDiverges && !qtyDiverges)
            return null;

        var correctedDeviation = evt.MarketAverage > 0
            ? Math.Round((nfUnit - evt.MarketAverage) / evt.MarketAverage * 100m, 2, MidpointRounding.AwayFromZero)
            : evt.DeviationPercentage;

        return new Result(nfUnit, nfQty, nfTotal, correctedDeviation, true);
    }

    private static ExpenseItem? FindMatchingItem(string? productName, IReadOnlyCollection<ExpenseItem> items)
    {
        var key = NormalizeMatchKey(productName);
        if (string.IsNullOrEmpty(key))
            return null;

        ExpenseItem? best = null;
        var bestScore = 0;

        foreach (var item in items)
        {
            var itemKey = NormalizeMatchKey(item.Description);
            if (string.IsNullOrEmpty(itemKey))
                continue;

            var score = ScoreMatch(key, itemKey);
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        return bestScore >= 60 ? best : null;
    }

    private static string NormalizeMatchKey(string? text) =>
        ProductCatalogNormalizer.Normalize(
            FinancialLineItemSemanticNormalizer.ToSearchQueryLabel(text ?? string.Empty, 96));

    private static int ScoreMatch(string a, string b)
    {
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (a.Contains(b, StringComparison.OrdinalIgnoreCase)
            || b.Contains(a, StringComparison.OrdinalIgnoreCase))
            return 80;

        var tokensA = a.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokensB = b.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokensA.Length == 0 || tokensB.Length == 0)
            return 0;

        var overlap = tokensA.Count(t => tokensB.Any(u => u.Equals(t, StringComparison.OrdinalIgnoreCase)));
        return (int)Math.Round(100.0 * overlap / Math.Max(tokensA.Length, tokensB.Length));
    }
}
