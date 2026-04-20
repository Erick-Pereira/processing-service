using System;
using System.Collections.Generic;
using Simcag.Shared.Events;

namespace Simcag.ProcessingService.Domain.Events;

/// <summary>
/// Evento disparado quando uma compra é processada
/// </summary>
public class PurchaseProcessedEvent : BaseEvent
{
    /// <summary>
    /// Identificador da compra
    /// </summary>
    public string PurchaseId { get; init; }

    /// <summary>
    /// Identificador do usuário
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Valor total da compra
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// Itens da compra
    /// </summary>
    public List<string> Items { get; init; } = new();

    /// <summary>
    /// Tipo do evento
    /// </summary>
    public override string EventType => "PurchaseProcessed";

    /// <summary>
    /// Construtor
    /// </summary>
    public PurchaseProcessedEvent(string purchaseId, string userId, decimal totalAmount, List<string> items)
    {
        PurchaseId = purchaseId;
        UserId = userId;
        TotalAmount = totalAmount;
        Items = items;
    }
}