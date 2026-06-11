using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>Extrai análises de preço persistidas na trilha de auditoria (PriceAnalyzed).</summary>
public static class ExpensePriceAnalysisBuilder
{
    public static IReadOnlyList<ExpensePriceAnalysisDto> BuildFromAudits(IReadOnlyList<AuditLog> auditsChronological)
    {
        var list = new List<ExpensePriceAnalysisDto>();
        foreach (var audit in auditsChronological.Where(a => a.Action == "PriceAnalyzed"))
        {
            var dto = TryParse(audit);
            if (dto is not null)
                list.Add(dto);
        }

        return list
            .OrderByDescending(x => x.AnalyzedAt)
            .ToList();
    }

    private static ExpensePriceAnalysisDto? TryParse(AuditLog audit)
    {
        if (string.IsNullOrWhiteSpace(audit.NewValue))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(audit.NewValue);
            var r = doc.RootElement;
            decimal Dec(string camel, string pascal)
            {
                if (r.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.Number)
                    return c.GetDecimal();
                if (r.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.Number)
                    return p.GetDecimal();
                return 0m;
            }

            string Str(string camel, string pascal)
            {
                if (r.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.String)
                    return c.GetString() ?? string.Empty;
                if (r.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.String)
                    return p.GetString() ?? string.Empty;
                return string.Empty;
            }

            int? IntOrNull(string camel, string pascal)
            {
                if (r.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var ci))
                    return ci;
                if (r.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pi))
                    return pi;
                return null;
            }

            decimal? DecOrNull(string camel, string pascal)
            {
                if (r.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.Number)
                    return c.GetDecimal();
                if (r.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.Number)
                    return p.GetDecimal();
                return null;
            }

            static IReadOnlyList<ExpenseMarketEvidenceDto> ParseEvidence(JsonElement root)
            {
                if (!root.TryGetProperty("marketEvidence", out var arr) && !root.TryGetProperty("MarketEvidence", out arr))
                    return Array.Empty<ExpenseMarketEvidenceDto>();
                if (arr.ValueKind != JsonValueKind.Array)
                    return Array.Empty<ExpenseMarketEvidenceDto>();

                var list = new List<ExpenseMarketEvidenceDto>();
                foreach (var item in arr.EnumerateArray())
                {
                    string EStr(string camel, string pascal)
                    {
                        if (item.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.String)
                            return c.GetString() ?? string.Empty;
                        if (item.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.String)
                            return p.GetString() ?? string.Empty;
                        return string.Empty;
                    }

                    list.Add(new ExpenseMarketEvidenceDto
                    {
                        Scope = EStr("scope", "Scope"),
                        Phase = EStr("phase", "Phase"),
                        Message = EStr("message", "Message"),
                        Detail = EStr("detail", "Detail"),
                    });
                }

                return list;
            }

            static IReadOnlyList<ExpenseMarketPriceSampleDto> ParseMarketSamples(JsonElement root)
            {
                if (!root.TryGetProperty("marketSamples", out var arr)
                    && !root.TryGetProperty("MarketSamples", out arr))
                    return Array.Empty<ExpenseMarketPriceSampleDto>();
                if (arr.ValueKind != JsonValueKind.Array)
                    return Array.Empty<ExpenseMarketPriceSampleDto>();

                var list = new List<ExpenseMarketPriceSampleDto>();
                foreach (var item in arr.EnumerateArray())
                {
                    string SStr(string camel, string pascal)
                    {
                        if (item.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.String)
                            return c.GetString() ?? string.Empty;
                        if (item.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.String)
                            return p.GetString() ?? string.Empty;
                        return string.Empty;
                    }

                    decimal? PriceOrNull(string camel, string pascal)
                    {
                        if (item.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.Number)
                            return c.GetDecimal();
                        if (item.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.Number)
                            return p.GetDecimal();
                        return null;
                    }

                    var label = SStr("label", "Label");
                    var url = SStr("url", "Url");
                    if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(url))
                        continue;

                    list.Add(new ExpenseMarketPriceSampleDto
                    {
                        Label = label,
                        Url = url,
                        PriceBrl = PriceOrNull("priceBrl", "PriceBrl"),
                        Provider = SStr("provider", "Provider"),
                    });
                }

                return list;
            }

            static IReadOnlyList<ExpenseMarketReferenceLinkDto> ParseReferenceLinks(JsonElement root)
            {
                if (!root.TryGetProperty("marketReferenceLinks", out var arr)
                    && !root.TryGetProperty("MarketReferenceLinks", out arr))
                    return Array.Empty<ExpenseMarketReferenceLinkDto>();
                if (arr.ValueKind != JsonValueKind.Array)
                    return Array.Empty<ExpenseMarketReferenceLinkDto>();

                var list = new List<ExpenseMarketReferenceLinkDto>();
                foreach (var item in arr.EnumerateArray())
                {
                    string LStr(string camel, string pascal)
                    {
                        if (item.TryGetProperty(camel, out var c) && c.ValueKind == JsonValueKind.String)
                            return c.GetString() ?? string.Empty;
                        if (item.TryGetProperty(pascal, out var p) && p.ValueKind == JsonValueKind.String)
                            return p.GetString() ?? string.Empty;
                        return string.Empty;
                    }

                    var url = LStr("url", "Url");
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    list.Add(new ExpenseMarketReferenceLinkDto
                    {
                        Label = LStr("label", "Label"),
                        Url = url,
                    });
                }

                return list;
            }

            bool Bool(string camel, string pascal)
            {
                if (r.TryGetProperty(camel, out var c) && (c.ValueKind == JsonValueKind.True || c.ValueKind == JsonValueKind.False))
                    return c.GetBoolean();
                if (r.TryGetProperty(pascal, out var p) && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False))
                    return p.GetBoolean();
                return false;
            }

            DateTime analyzedAt = audit.CreatedAt;
            if (r.TryGetProperty("analysisDate", out var ad) && ad.ValueKind == JsonValueKind.String
                && DateTime.TryParse(ad.GetString(), out var parsed))
                analyzedAt = parsed;
            else if (r.TryGetProperty("AnalysisDate", out var ad2) && ad2.ValueKind == JsonValueKind.String
                     && DateTime.TryParse(ad2.GetString(), out var parsed2))
                analyzedAt = parsed2;

            var productId = Str("productId", "ProductId");
            if (string.IsNullOrWhiteSpace(productId))
                return null;

            return new ExpensePriceAnalysisDto
            {
                ProductId = productId,
                ProductName = Str("productName", "ProductName"),
                Category = Str("category", "Category"),
                LastPrice = Dec("lastPrice", "LastPrice"),
                Quantity = IntOrNull("quantity", "Quantity"),
                LineTotal = DecOrNull("lineTotal", "LineTotal"),
                MarketAverage = Dec("marketAverage", "MarketAverage"),
                HistoricalAverage = Dec("historicalAverage", "HistoricalAverage"),
                DeviationPercentage = Dec("deviationPercentage", "DeviationPercentage"),
                Severity = Str("severity", "Severity"),
                AnalyzedAt = analyzedAt,
                Source = "price-analysis",
                MarketSource = Str("marketSource", "MarketSource"),
                MarketBenchmarkKind = Str("marketBenchmarkKind", "MarketBenchmarkKind"),
                MarketBenchmarkStatus = Str("marketBenchmarkStatus", "MarketBenchmarkStatus"),
                MarketConfidence = Str("marketConfidence", "MarketConfidence"),
                MarketSampleCount = IntOrNull("marketSampleCount", "MarketSampleCount"),
                MarketRelativeSpread = DecOrNull("marketRelativeSpread", "MarketRelativeSpread"),
                MarketSearchQuery = Str("marketSearchQuery", "MarketSearchQuery"),
                MarketDocumentAnchorPrice = DecOrNull("marketDocumentAnchorPrice", "MarketDocumentAnchorPrice"),
                MarketEvidence = ParseEvidence(r),
                MarketReferenceLinks = ParseReferenceLinks(r),
                MarketSamples = ParseMarketSamples(r),
                NfUnitPrice = DecOrNull("nfUnitPrice", "NfUnitPrice"),
                NfQuantity = IntOrNull("nfQuantity", "NfQuantity"),
                NfLineTotal = DecOrNull("nfLineTotal", "NfLineTotal"),
                PriceAuditCorrected = Bool("priceAuditCorrected", "PriceAuditCorrected"),
            };
        }
        catch
        {
            return null;
        }
    }
}
