using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simcag.ProcessingService.Application.Configuration;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.Shared.Telemetry;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

/// <summary>
/// Worker que drena a outbox para o RabbitMQ com retries, estado poison e recuperação de
/// linhas <see cref="MessageOutboxStatus.Dispatching"/> com lock expirado (crash após publish ou antes do commit).
/// Publicação para o broker é <i>at least once</i>; consumidores devem manter inbox idempotente por
/// <c>transport_message_id</c>. Múltiplas instâncias do relay podem duplicar publicação até ao mesmo
/// <see cref="MessageOutbox.MessageId"/> — o mesmo modelo de idempotência aplica-se.
/// </summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OutboxRelayOptions> _options;
    private readonly ILogger<OutboxRelayWorker> _log;

    public OutboxRelayWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxRelayOptions> options,
        ILogger<OutboxRelayWorker> log)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("OutboxRelayWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var delayMs = opts.IdlePollIntervalMilliseconds;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<OutboxRelayRunner>();
                var processed = await runner.ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
                delayMs = processed > 0
                    ? opts.PollIntervalMilliseconds
                    : opts.IdlePollIntervalMilliseconds;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ciclo do OutboxRelayWorker falhou.");
                delayMs = opts.PollIntervalMilliseconds;
            }

            try
            {
                await Task.Delay(delayMs, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("OutboxRelayWorker cancelado.");
    }
}

internal sealed class OutboxRelayRunner
{
    private readonly ProcessingDbContext _db;
    private readonly OutboxRelayDispatcher _dispatcher;
    private readonly IOptions<OutboxRelayOptions> _options;
    private readonly ILogger<OutboxRelayRunner> _log;

    public OutboxRelayRunner(
        ProcessingDbContext db,
        OutboxRelayDispatcher dispatcher,
        IOptions<OutboxRelayOptions> options,
        ILogger<OutboxRelayRunner> log)
    {
        _db = db;
        _dispatcher = dispatcher;
        _options = options;
        _log = log;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        var now = DateTime.UtcNow;
        var lockDuration = TimeSpan.FromSeconds(Math.Clamp(opts.ClaimLockSeconds, 5, 300));
        var maxRows = Math.Clamp(opts.BatchSize, 1, 500);
        var processed = 0;

        for (var i = 0; i < maxRows; i++)
        {
            var row = await _db.MessageOutboxes
                .Where(m =>
                    (m.Status == MessageOutboxStatus.Pending || m.Status == MessageOutboxStatus.Dispatching)
                    && m.AttemptCount < m.MaxAttempts
                    && m.NextAttemptAtUtc <= now
                    && (m.LockedUntilUtc == null || m.LockedUntilUtc < now))
                .OrderBy(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
                return processed;

            processed++;

            if (row.Status == MessageOutboxStatus.Dispatching)
                row.ReclaimStaleDispatching(now, lockDuration);
            else
                row.MarkDispatching(now, lockDuration);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            using var activity = SimcagActivitySources.Messaging.StartActivity("outbox.relay.dispatch", System.Diagnostics.ActivityKind.Internal);
            activity?.SetTag("simcag.outbox_id", row.Id.ToString());
            activity?.SetTag("simcag.event_type", row.EventType);
            activity?.SetTag("messaging.message_id", row.MessageId.ToString());

            try
            {
                await _dispatcher.DispatchAsync(row, cancellationToken).ConfigureAwait(false);
                row.MarkPublished(DateTime.UtcNow);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                SimcagMeters.MessagingOutboxRelayPublishedTotal.Add(1,
                    new KeyValuePair<string, object?>("event_type", row.EventType));
                _log.LogInformation(
                    "Outbox {OutboxId} publicada (event={EventType}, messageId={MessageId}).",
                    row.Id,
                    row.EventType,
                    row.MessageId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Falha ao publicar outbox {OutboxId} tentativa {Attempt}/{Max}.",
                    row.Id,
                    row.AttemptCount,
                    row.MaxAttempts);

                if (row.AttemptCount >= row.MaxAttempts)
                {
                    row.MarkPoisoned(ex.ToString(), DateTime.UtcNow);
                    SimcagMeters.MessagingOutboxRelayPoisonedTotal.Add(1,
                        new KeyValuePair<string, object?>("event_type", row.EventType));
                    _log.LogError(
                        "Outbox {OutboxId} poison após {Attempts} tentativas.",
                        row.Id,
                        row.AttemptCount);
                }
                else
                {
                    var backoff = TimeSpan.FromSeconds(
                        Math.Min(
                            3600,
                            Math.Pow(2, Math.Min(row.AttemptCount, 10)) * Math.Max(1, opts.RetryBackoffBaseSeconds)));
                    row.ScheduleRetry(ex.Message, DateTime.UtcNow, backoff);
                }

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return processed;
    }
}
