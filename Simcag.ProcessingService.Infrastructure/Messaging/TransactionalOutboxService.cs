using System.Text.Json;
using Microsoft.Extensions.Options;
using Simcag.ProcessingService.Application.Configuration;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging.Contracts;
using Simcag.Shared.Messaging.RabbitMQ;
using Simcag.Shared.Messaging.RabbitMQ.Internal;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

public sealed class TransactionalOutboxService : ITransactionalOutbox
{
    private readonly ProcessingDbContext _db;
    private readonly IOptions<OutboxRelayOptions> _relayOptions;

    public TransactionalOutboxService(ProcessingDbContext db, IOptions<OutboxRelayOptions> relayOptions)
    {
        _db = db;
        _relayOptions = relayOptions;
    }

    public void Enqueue<TEvent>(TEvent @event, string? dedupeKey = null) where TEvent : BaseEvent
    {
        if (@event is not IEvent ievent)
            throw new InvalidOperationException($"Evento {typeof(TEvent).Name} não implementa IEvent.");

        var tenant = EventTenantExtractor.TryGetTenantGuid(ievent)
                     ?? throw new InvalidOperationException(
                         $"Outbox requer tenant explícito no evento ({typeof(TEvent).Name}).");

        var envelope = RabbitMqMessageEnvelopeFactory.Create(ievent, publishActivity: null);
        var json = JsonSerializer.Serialize(envelope, RabbitMqJsonSerializerOptions.Instance);

        var row = MessageOutbox.CreatePending(
            tenantId: tenant,
            messageId: envelope.MessageId,
            dedupeKey: dedupeKey,
            eventType: typeof(TEvent).Name,
            routingKey: typeof(TEvent).Name,
            payloadJson: json,
            correlationId: envelope.CorrelationId,
            traceParent: envelope.TraceParent,
            traceState: envelope.TraceState,
            baggage: envelope.Baggage,
            maxAttempts: _relayOptions.Value.MaxPublishAttempts,
            utcNow: DateTime.UtcNow);

        _db.MessageOutboxes.Add(row);
    }
}
