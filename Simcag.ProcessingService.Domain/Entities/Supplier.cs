using System;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Fornecedor condominial — agregado raiz canônico v1.
/// Identificação canônica por <see cref="Cnpj"/> (preferencial) ou
/// <see cref="NormalizedName"/> + <see cref="CondominioId"/> (fallback).
/// </summary>
public sealed class Supplier
{
    public Guid Id { get; private set; }

    /// <summary>Tenant (null = fornecedor global, ex.: cadastrado pelo admin).</summary>
    public Guid? CondominioId { get; private set; }

    /// <summary>CNPJ (apenas dígitos). Único globalmente quando presente.</summary>
    public string? Cnpj { get; private set; }

    /// <summary>Nome normalizado (uppercase, sem acento, espaços normalizados).</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Categoria predominante.</summary>
    public string? Category { get; private set; }

    /// <summary>Soft delete.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Supplier() { }

    public static Supplier Create(
        Guid? condominioId,
        string normalizedName,
        string? cnpj = null,
        string? category = null)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("NormalizedName obrigatório.");

        var now = DateTime.UtcNow;
        return new Supplier
        {
            Id = Guid.NewGuid(),
            CondominioId = condominioId,
            Cnpj = NormalizeCnpj(cnpj),
            NormalizedName = NormalizeName(normalizedName),
            Category = category,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string? cnpj, string normalizedName, string? category)
    {
        Cnpj = NormalizeCnpj(cnpj) ?? Cnpj;
        NormalizedName = NormalizeName(normalizedName);
        Category = category ?? Category;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeCnpj(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj)) return null;
        var digits = new string([.. cnpj.Where(char.IsDigit)]);
        return digits.Length == 14 ? digits : null;
    }

    private static string NormalizeName(string raw)
    {
        var s = raw.Trim().ToUpperInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ");
    }
}
