using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.Messaging.RabbitMQ;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

/// <summary>Despacha linhas da outbox para o broker usando o envelope JSON persistido.</summary>
internal sealed class OutboxRelayDispatcher
{
    private readonly RabbitMqOutboxRelayTransport _transport;
    private readonly ILogger<OutboxRelayDispatcher> _log;

    public OutboxRelayDispatcher(
        RabbitMqOutboxRelayTransport transport,
        ILogger<OutboxRelayDispatcher> log)
    {
        _transport = transport;
        _log = log;
    }

    public Task DispatchAsync(MessageOutbox row, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.PayloadJson))
        {
            _log.LogError("Outbox {OutboxId} sem payload JSON.", row.Id);
            throw new InvalidOperationException($"Outbox {row.Id} payload vazio.");
        }

        return _transport.PublishSerializedEnvelopeAsync(
            row.PayloadJson,
            row.RoutingKey,
            row.MessageId,
            row.CorrelationId,
            row.TraceParent,
            row.TraceState,
            row.Baggage,
            row.EventType,
            row.CreatedAtUtc,
            cancellationToken);
    }
}
