namespace Simcag.ProcessingService.Domain.ValueObjects;

/// <summary>
/// Informações de contato de um fornecedor (e-mail, telefone, endereço).
/// Imutável; criada somente via construtor para permitir mapeamento como Owned Entity no EF.
/// </summary>
public sealed class ContactInfo
{
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }

    private ContactInfo() { }

    public ContactInfo(string? email, string? phone, string? address)
    {
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : new string([.. phone.Where(c => char.IsDigit(c) || c == '+')]);
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    public static ContactInfo Empty() => new(null, null, null);
}
