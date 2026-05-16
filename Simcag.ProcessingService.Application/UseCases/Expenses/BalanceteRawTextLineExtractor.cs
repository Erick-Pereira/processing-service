using System.Globalization;
using System.Text.RegularExpressions;
using Simcag.Shared.Events;
using Simcag.Shared.Finance;

namespace Simcag.ProcessingService.Application.UseCases.Expenses;

/// <summary>
/// Espelha a extração compacta do ingestion (ParseDocumentUseCase) quando o evento chega sem
/// <c>ExtractedFields.Lines</c> preenchido.
/// </summary>
internal static class BalanceteRawTextLineExtractor
{
    private static readonly Regex RowRx = new(
        @"(Manutenção|Manutencao|Serviços|Servicos|Utilidades|Administrativo|Outros)(.+?)(\d{1,3}(?:\.\d{3})*,\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Tenta produzir linhas a partir do texto bruto do PDF (tabela colada).</summary>
    public static IReadOnlyList<IngestedExpenseLine>? TryExtractLines(string? rawText, string? documentType)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        if (!ShouldAttemptExtraction(rawText, documentType))
            return null;

        if (Regex.Matches(rawText, @"\d{1,3}(?:\.\d{3})*,\d{2}").Count < 3)
            return null;

        var scanText = TrySliceAfterTableHeader(rawText) ?? rawText;
        var list = new List<IngestedExpenseLine>();

        foreach (Match m in RowRx.Matches(scanText))
        {
            if (!m.Success || m.Groups.Count < 4)
                continue;

            var cat = m.Groups[1].Value.Trim();
            var desc = m.Groups[2].Value.Trim();
            var amtRaw = m.Groups[3].Value;

            if (desc.Contains("total", StringComparison.OrdinalIgnoreCase)
                && desc.Contains("gasto", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsGluedCondominioJunkDescription(desc))
                continue;

            if (!TryParseBrazilianMoney(amtRaw, out var amt))
                continue;

            var fullDesc = $"{cat} — {desc}".Trim();
            if (fullDesc.Length > 500)
                fullDesc = fullDesc[..500];

            var normalized = FinancialLineItemSemanticNormalizer.NormalizeFinancialItem(
                new FinancialItem { Description = fullDesc, Amount = amt });

            list.Add(new IngestedExpenseLine { Description = normalized.Description, Amount = normalized.Amount });
        }

        return list.Count > 0 ? list : null;
    }

    private static bool ShouldAttemptExtraction(string rawText, string? documentType)
    {
        var doc = documentType ?? string.Empty;
        if (doc.Contains("BALANCE", StringComparison.OrdinalIgnoreCase))
            return true;

        var u = rawText.ToUpperInvariant();
        if (!u.Contains("CONDOM", StringComparison.Ordinal) && !u.Contains("CONDOMINIO", StringComparison.Ordinal))
            return false;

        return u.Contains("RELAT", StringComparison.Ordinal)
               || u.Contains("GASTO", StringComparison.Ordinal)
               || u.Contains("DESPESA", StringComparison.Ordinal);
    }

    private static string? TrySliceAfterTableHeader(string rawText)
    {
        var markers = new[]
        {
            "Valor (R$)", "Valor(R$)", "VALOR (R$)", "Valor( R$ )",
            "CategoriaDescriçãoValor (R$)", "CategoriaDescricaoValor (R$)"
        };
        foreach (var mk in markers)
        {
            var idx = rawText.IndexOf(mk, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return rawText.AsSpan(idx + mk.Length).ToString();
        }

        return null;
    }

    private static bool IsGluedCondominioJunkDescription(string desc)
    {
        if (desc.Length > 140)
            return true;

        var u = desc.ToUpperInvariant();
        if (u.Contains("CATEGORIADESCRI", StringComparison.Ordinal))
            return true;
        if (u.Contains("REFERENTES AO MÊS", StringComparison.Ordinal) || u.Contains("REFERENTES AO MES", StringComparison.Ordinal))
            return true;
        if (u.Contains("VALOR (R$)", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool TryParseBrazilianMoney(string raw, out decimal amount)
    {
        amount = 0;
        raw = raw.Trim();
        if (string.IsNullOrEmpty(raw))
            return false;

        var hasComma = raw.Contains(',');
        var hasDot = raw.Contains('.');
        string normalized;
        if (hasComma && hasDot)
            normalized = raw.Replace(".", "", StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal);
        else if (hasComma && !hasDot)
            normalized = raw.Replace(",", ".", StringComparison.Ordinal);
        else if (!hasComma && hasDot)
        {
            var parts = raw.Split('.');
            if (parts.Length > 1 && parts[^1].Length == 3 && parts[^1].All(char.IsDigit))
                normalized = raw.Replace(".", "", StringComparison.Ordinal);
            else
                normalized = raw;
        }
        else
            normalized = raw;

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
               && amount > 0;
    }
}
