namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>
/// Espelho legado da despesa para índices SQL, filtros antigos e clientes que só conhecem um campo <c>status</c>.
/// Mantido sincronizado com <see cref="ExpenseApprovalStatus"/>, <see cref="ExpenseSettlementStatus"/> e falhas de processamento.
/// </summary>
public enum ExpenseStatus
{
    /// <summary>Legado: aguardando aprovação humana (ou processamento ainda não pronto para fila de aprovação).</summary>
    Pending = 1,

    Approved = 2,
    Paid = 3,
    Cancelled = 4,

    /// <summary>Rejeitada na aprovação humana.</summary>
    Rejected = 5,

    /// <summary>Falha técnica na pipeline (espelho de <see cref="ExpenseProcessingStatus.Failed"/>).</summary>
    ProcessingFailed = 6,
}
