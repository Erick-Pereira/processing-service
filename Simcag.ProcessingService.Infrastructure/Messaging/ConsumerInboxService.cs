using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Infrastructure.Persistence;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

/// <summary>
/// Inbox com <c>INSERT … ON CONFLICT DO NOTHING</c> para não poluir o change tracker
/// nem exigir <c>ChangeTracker.Clear()</c> dentro de transações longas.
/// </summary>
public sealed class ConsumerInboxService : IConsumerInbox
{
    private readonly ProcessingDbContext _db;

    public ConsumerInboxService(ProcessingDbContext db) => _db = db;

    public async Task<bool> TryReserveAsync(
        string consumerGroup,
        Guid transportMessageId,
        Guid tenantId,
        Guid? domainEventId,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var inserted = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO consumer_inbox (id, tenant_id, consumer_group, transport_message_id, domain_event_id, status, received_at_utc, completed_at_utc, attempt_count, last_error)
             VALUES ({id}, {tenantId}, {consumerGroup}, {transportMessageId}, {domainEventId}, {"Pending"}, {now}, NULL, {0}, NULL)
             ON CONFLICT (consumer_group, transport_message_id) DO NOTHING
             """,
            cancellationToken);
        return inserted > 0;
    }

    public async Task MarkCompletedAsync(
        string consumerGroup,
        Guid transportMessageId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE consumer_inbox
             SET status = {"Completed"}, completed_at_utc = {now}
             WHERE consumer_group = {consumerGroup} AND transport_message_id = {transportMessageId}
             """,
            cancellationToken);
    }
}
