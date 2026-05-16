using Simcag.Shared.Events;

namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Enfileira evento na outbox ligada ao <c>ProcessingDbContext</c> atual.
/// O caller deve invocar <c>SaveChangesAsync</c> na mesma transação.
/// </summary>
public interface ITransactionalOutbox
{
    void Enqueue<TEvent>(TEvent @event, string? dedupeKey = null)
        where TEvent : BaseEvent;
}
