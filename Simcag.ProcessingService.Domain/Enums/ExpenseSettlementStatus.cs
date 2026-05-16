namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Liquidação financeira derivada de pagamentos (separado de aprovação).</summary>
public enum ExpenseSettlementStatus
{
    Unpaid = 1,
    PartiallyPaid = 2,
    Paid = 3,
}
