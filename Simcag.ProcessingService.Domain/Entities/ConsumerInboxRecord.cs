using System;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Inbox por consumidor: garante exatamente-uma vez de efeito por
/// <c>(consumer_group, transport_message_id)</c> (id da mensagem AMQP / envelope).
/// </summary>
public sealed class ConsumerInboxRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ConsumerGroup { get; private set; } = string.Empty;
    public Guid TransportMessageId { get; private set; }
    public Guid? DomainEventId { get; private set; }
    public ConsumerInboxStatus Status { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    private ConsumerInboxRecord() { }

    public static ConsumerInboxRecord CreatePending(
        Guid tenantId,
        string consumerGroup,
        Guid transportMessageId,
        Guid? domainEventId,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId obrigatório.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(consumerGroup)) throw new ArgumentException("ConsumerGroup obrigatório.", nameof(consumerGroup));
        if (transportMessageId == Guid.Empty) throw new ArgumentException("TransportMessageId obrigatório.", nameof(transportMessageId));

        return new ConsumerInboxRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConsumerGroup = consumerGroup.Trim(),
            TransportMessageId = transportMessageId,
            DomainEventId = domainEventId,
            Status = ConsumerInboxStatus.Pending,
            ReceivedAtUtc = utcNow,
            AttemptCount = 0,
        };
    }

    public void MarkCompleted(DateTime utcNow)
    {
        Status = ConsumerInboxStatus.Completed;
        CompletedAtUtc = utcNow;
    }
}
