namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Ciclo de vida de aprovação humana (síndico/conselho), independente da pipeline técnica.</summary>
public enum ExpenseApprovalStatus
{
    /// <summary>Aguarda decisão humana.</summary>
    PendingApproval = 1,

    /// <summary>Aprovada para pagamento / execução.</summary>
    Approved = 2,

    /// <summary>Rejeitada explicitamente (não confundir com cancelamento operacional).</summary>
    Rejected = 3,

    /// <summary>Cancelada (motivo operacional / administrativo).</summary>
    Cancelled = 4,
}
