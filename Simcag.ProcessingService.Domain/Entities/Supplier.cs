using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.ProcessingService.Domain.ValueObjects;
using Simcag.Shared.Auditing;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Fornecedor condominial — agregado raiz canônico.
/// Identificado por <see cref="Document"/> (CNPJ ou CPF, normalizado para apenas dígitos)
/// e único dentro do escopo <see cref="TenantId"/>.
/// Implementa <see cref="IAuditableEntity"/>: criação, atualização e desativação são auditadas.
/// </summary>
public sealed class Supplier : IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Documento somente dígitos (CNPJ=14 ou CPF=11).</summary>
    public string Document { get; private set; } = string.Empty;

    public SupplierDocumentType DocumentType { get; private set; }

    public ContactInfo Contact { get; private set; } = ContactInfo.Empty();

    public string? Category { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // NOVO: Score de confiança na identificação do fornecedor (0.00 a 1.00).
    public decimal? IdentificationConfidenceScore { get; private set; }

    private Supplier() { }

    public static Supplier Create(
        Guid tenantId,
        string name,
        string document,
        ContactInfo? contact = null,
        string? category = null,
        decimal? confidenceScore = null)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId obrigatório.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome do fornecedor obrigatório.");

        var (digits, type) = NormalizeDocument(document);
        var now = DateTime.UtcNow;

        return new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            NormalizedName = NormalizeName(name),
            Document = digits,
            DocumentType = type,
            Contact = contact ?? ContactInfo.Empty(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            IdentificationConfidenceScore = confidenceScore
        };
    }

    public void Update(string name, string document, ContactInfo? contact, string? category, decimal? confidenceScore)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nome obrigatório.");
        var (digits, type) = NormalizeDocument(document);

        Name = name.Trim();
        NormalizedName = NormalizeName(name);
        Document = digits;
        DocumentType = type;
        if (contact is not null) Contact = contact;
        Category = string.IsNullOrWhiteSpace(category) ? Category : category.Trim();
        UpdatedAt = DateTime.UtcNow;
        // Atualiza o score de identificação sempre que o fornecedor é atualizado
        IdentificationConfidenceScore = confidenceScore;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public static (string digits, SupplierDocumentType type) NormalizeDocument(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new DomainException("Documento obrigatório.");
        var digits = new string([.. raw.Where(char.IsDigit)]);
        return digits.Length switch
        {
            14 => (digits, SupplierDocumentType.Cnpj),
            11 => (digits, SupplierDocumentType.Cpf),
            _ => throw new DomainException("Documento inválido: deve conter 11 (CPF) ou 14 (CNPJ) dígitos."),
        };
    }

    public static string NormalizeName(string raw)
    {
        var s = raw.Trim().ToUpperInvariant();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ");
    }
}
