using System;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Domain.Entities;

/// <summary>
/// Outbox transacional: a mensagem é escrita na mesma transação que o agregado de domínio
/// e enviada ao broker por um worker separado (atomicidade DB → broker).
/// </summary>
public sealed class MessageOutbox
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Id da mensagem no envelope AMQP (estável entre retries do relay).</summary>
    public Guid MessageId { get; private set; }

    /// <summary>Chave opcional de dedupe de negócio (ex.: mesmo documento).</summary>
    public string? DedupeKey { get; private set; }

    public string EventType { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;

    /// <summary>JSON do <c>MessageEnvelope&lt;T&gt;</c> pronto a publicar.</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    public string? CorrelationId { get; private set; }
    public string? TraceParent { get; private set; }
    public string? TraceState { get; private set; }
    public string? Baggage { get; private set; }

    public MessageOutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? PoisonedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private MessageOutbox() { }

    public static MessageOutbox CreatePending(
        Guid tenantId,
        Guid messageId,
        string? dedupeKey,
        string eventType,
        string routingKey,
        string payloadJson,
        string? correlationId,
        string? traceParent,
        string? traceState,
        string? baggage,
        int maxAttempts,
        DateTime utcNow)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId obrigatório.", nameof(tenantId));
        if (messageId == Guid.Empty) throw new ArgumentException("MessageId obrigatório.", nameof(messageId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType obrigatório.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(routingKey)) throw new ArgumentException("RoutingKey obrigatório.", nameof(routingKey));
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new ArgumentException("PayloadJson obrigatório.", nameof(payloadJson));

        return new MessageOutbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = messageId,
            DedupeKey = string.IsNullOrWhiteSpace(dedupeKey) ? null : dedupeKey.Trim(),
            EventType = eventType.Trim(),
            RoutingKey = routingKey.Trim(),
            PayloadJson = payloadJson,
            CorrelationId = correlationId,
            TraceParent = traceParent,
            TraceState = traceState,
            Baggage = baggage,
            Status = MessageOutboxStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts < 1 ? 12 : maxAttempts,
            NextAttemptAtUtc = utcNow,
            LockedUntilUtc = null,
            CreatedAtUtc = utcNow,
        };
    }

    public void MarkDispatching(DateTime utcNow, TimeSpan lockDuration)
    {
        Status = MessageOutboxStatus.Dispatching;
        AttemptCount++;
        LockedUntilUtc = utcNow.Add(lockDuration);
        NextAttemptAtUtc = utcNow.Add(lockDuration);
    }

    /// <summary>
    /// Worker morreu após publicar ou durante <c>SaveChanges</c>: lock expirou com estado
    /// <see cref="MessageOutboxStatus.Dispatching"/>. Reclama sem incrementar <see cref="AttemptCount"/>
    /// outra vez para a mesma tentativa lógica de publicação.
    /// </summary>
    public void ReclaimStaleDispatching(DateTime utcNow, TimeSpan lockDuration)
    {
        Status = MessageOutboxStatus.Dispatching;
        LockedUntilUtc = utcNow.Add(lockDuration);
        NextAttemptAtUtc = utcNow.Add(lockDuration);
    }

    public void MarkPublished(DateTime utcNow)
    {
        Status = MessageOutboxStatus.Published;
        PublishedAtUtc = utcNow;
        LockedUntilUtc = null;
        LastError = null;
    }

    public void ScheduleRetry(string error, DateTime utcNow, TimeSpan delay)
    {
        Status = MessageOutboxStatus.Pending;
        LockedUntilUtc = null;
        LastError = error.Length > 2000 ? error[..2000] : error;
        NextAttemptAtUtc = utcNow.Add(delay);
    }

    public void MarkPoisoned(string error, DateTime utcNow)
    {
        Status = MessageOutboxStatus.Poisoned;
        PoisonedAtUtc = utcNow;
        LockedUntilUtc = null;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
