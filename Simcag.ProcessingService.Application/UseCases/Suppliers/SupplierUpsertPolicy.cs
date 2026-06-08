using System.Security.Cryptography;
using System.Text;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Application.UseCases.Suppliers;

/// <summary>
/// Regras de upsert de fornecedor — evita sobrescrever razão social de cadastros existentes
/// quando o mesmo CNPJ aparece com nome diferente (homologação SEFAZ ou re-upload).
/// </summary>
public static class SupplierUpsertPolicy
{
    private const string PlaceholderDocument = "00000000000000";

    public static bool IsPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;
        var n = name.Trim();
        return n.Equals("Fornecedor não identificado", StringComparison.OrdinalIgnoreCase)
               || n.Equals("Fornecedor nao identificado", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Documento sintético (14 dígitos) para fornecedor identificado por nome quando o CNPJ real já pertence a outra razão social.
    /// </summary>
    public static string BuildSyntheticDocumentForName(string normalizedName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName));
        var digits = new StringBuilder(14);
        digits.Append("88"); // prefixo reservado — não confundir com CNPJ real
        foreach (var b in hash)
        {
            if (digits.Length >= 14)
                break;
            digits.Append((b % 10).ToString());
        }

        while (digits.Length < 14)
            digits.Append('0');

        return digits.ToString();
    }

    public static bool ShouldUpdateExisting(Supplier existing, string incomingName)
    {
        var incomingNorm = Supplier.NormalizeName(incomingName);
        if (existing.NormalizedName == incomingNorm)
            return true;
        return IsPlaceholderName(existing.Name);
    }

    public static bool IsRealTaxDocument(string documentDigits) =>
        documentDigits.Length is 11 or 14
        && documentDigits != PlaceholderDocument
        && !documentDigits.StartsWith("88", StringComparison.Ordinal);
}
