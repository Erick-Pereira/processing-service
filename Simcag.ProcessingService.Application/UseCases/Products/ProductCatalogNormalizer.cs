using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Simcag.ProcessingService.Application.UseCases.Products;

/// <summary>
/// Normalização usada para agrupar linhas de despesa no catálogo de produtos.
/// Deve ser a mesma chave usada ao indexar benchmarks de mercado em <see cref="Domain.Entities.Product"/>.
/// </summary>
public static class ProductCatalogNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "sem-descricao";

        var withoutAccents = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                withoutAccents.Append(ch);
        }

        var compact = Regex.Replace(withoutAccents.ToString().ToUpperInvariant(), @"[^A-Z0-9]+", "-");
        return Regex.Replace(compact, @"-+", "-").Trim('-').ToLowerInvariant();
    }
}
