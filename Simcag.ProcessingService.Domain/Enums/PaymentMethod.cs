namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Modalidade de pagamento registrada para uma despesa.</summary>
public enum PaymentMethod
{
    Pix = 1,
    Boleto = 2,
    BankTransfer = 3,
    Cash = 4,
    Card = 5,
    Other = 99,
}
