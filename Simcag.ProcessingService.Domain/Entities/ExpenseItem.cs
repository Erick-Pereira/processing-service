using System;
using Simcag.ProcessingService.Domain.Exceptions;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Item de uma despesa. Entity child do agregado <see cref="Expense"/>.
/// Não possui repositório próprio — manipulação somente via <c>Expense.AddItem()</c> / <c>Expense.RemoveItem()</c>.
/// </summary>
public sealed class ExpenseItem
{
    public Guid Id { get; private set; }
    public Guid ExpenseId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>Total da linha = Quantity * UnitPrice. Mapeado como coluna calculada (gerada pelo PostgreSQL).</summary>
    public decimal TotalPrice { get; private set; }

    private ExpenseItem() { }

    internal static ExpenseItem Create(Guid expenseId, string description, decimal quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Descrição do item é obrigatória.");
        if (quantity <= 0m)
            throw new DomainException("Quantidade do item deve ser > 0.");
        if (unitPrice < 0m)
            throw new DomainException("Preço unitário não pode ser negativo.");

        return new ExpenseItem
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            Description = description.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = quantity * unitPrice,
        };
    }
}
