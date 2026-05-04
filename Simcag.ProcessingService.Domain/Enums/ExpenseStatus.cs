namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Estados possíveis de uma despesa em seu ciclo de vida.</summary>
public enum ExpenseStatus
{
    /// <summary>Cadastrada, aguardando aprovação do síndico/conselho.</summary>
    Pending = 1,
    /// <summary>Aprovada, pode receber pagamentos.</summary>
    Approved = 2,
    /// <summary>Totalmente paga (soma de pagamentos = TotalAmount).</summary>
    Paid = 3,
    /// <summary>Cancelada pelo administrador (não permite pagamentos).</summary>
    Cancelled = 4,
}
