using Simcag.ProcessingService.Application.Interfaces;
using Simcag.Shared.Events;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

/// <summary>
/// Publicação via outbox transacional: só persiste no broker após <c>SaveChanges</c> commit
/// e processamento pelo <see cref="OutboxRelayWorker"/>.
/// </summary>
public sealed class TransactionalOutboxEventPublisherAdapter : IEventPublisher
{
    private readonly ITransactionalOutbox _outbox;

    public TransactionalOutboxEventPublisherAdapter(ITransactionalOutbox outbox) => _outbox = outbox;

    public Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default)
        where T : BaseEvent
    {
        _outbox.Enqueue(eventMessage);
        return Task.CompletedTask;
    }
}
