using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.Messaging;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Enums;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging;
using Simcag.Shared.Messaging.Contracts;
using Simcag.Shared.Messaging.Telemetry;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Api.Workers;

/// <summary>
/// Consome <see cref="PriceAnalyzedEvent"/> do price-analysis e grava auditoria ligada à
/// <see cref="Expense"/> encontrada via <c>RawDocumentId</c> (documento de ingestão).
/// Inbox transacional por <c>envelope.MessageId</c> garante dedupe em reentregas RabbitMQ.
/// </summary>
public sealed class PriceAnalyzedConsumer : BackgroundService
{
    private readonly IEventConsumer<PriceAnalyzedEvent> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceAnalyzedConsumer> _log;

    private const int MaxRetries = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public PriceAnalyzedConsumer(
        IEventConsumer<PriceAnalyzedEvent> consumer,
        IServiceScopeFactory scopeFactory,
        ILogger<PriceAnalyzedConsumer> log)
    {
        _consumer = consumer;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("PriceAnalyzedConsumer iniciado (fila {Queue}).", EventBusConstants.QueuePriceAnalyzed);

        try
        {
            await foreach (var envelope in _consumer.ReadMessagesAsync(stoppingToken))
            {
                using (MessagingConsumeTelemetry.BeginConsume(envelope, out _))
                    await HandleEnvelopeAsync(envelope, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("PriceAnalyzedConsumer cancelado.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erro crítico no PriceAnalyzedConsumer; o BackgroundService irá reiniciar.");
            throw;
        }
    }

    private async Task HandleEnvelopeAsync(MessageEnvelope<PriceAnalyzedEvent> envelope, CancellationToken ct)
    {
        var evt = envelope.Data;
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var status = await ProcessAsync(envelope, ct);

                switch (status)
                {
                    case ProcessOutcome.Ok:
                    case ProcessOutcome.Skip:
                    case ProcessOutcome.AlreadyProcessed:
                        await _consumer.AcknowledgeMessageAsync(envelope, ct);
                        return;

                    case ProcessOutcome.Invalid:
                        _log.LogWarning("PriceAnalyzedEvent {EventId} inválido; rejeitando sem requeue.", evt.EventId);
                        await _consumer.RejectMessageAsync(envelope, ct);
                        return;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Falha ao processar PriceAnalyzedEvent {EventId} (tentativa {Attempt}/{Max}).",
                    evt.EventId, attempt, MaxRetries);

                if (attempt >= MaxRetries)
                {
                    await _consumer.RejectMessageAsync(envelope, ct);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }
    }

    private async Task<ProcessOutcome> ProcessAsync(MessageEnvelope<PriceAnalyzedEvent> envelope, CancellationToken ct)
    {
        var evt = envelope.Data;
        if (!IsValid(evt))
            return ProcessOutcome.Invalid;

        if (!Guid.TryParse(evt.TenantId, out var tenantId) || tenantId == Guid.Empty)
        {
            _log.LogWarning("PriceAnalyzedEvent {EventId}: TenantId inválido '{TenantId}'.", evt.EventId, evt.TenantId);
            return ProcessOutcome.Invalid;
        }

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var ambient = sp.GetRequiredService<AmbientPrincipal>();
        ambient.Set(tenantId: tenantId, userId: null, userName: "price-analysis");

        var db = sp.GetRequiredService<ProcessingDbContext>();
        var inbox = sp.GetRequiredService<IConsumerInbox>();

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var reserved = await inbox.TryReserveAsync(
                    ProcessingConsumerGroups.PriceAnalyzed,
                    envelope.MessageId,
                    tenantId,
                    evt.EventId,
                    ct)
                .ConfigureAwait(false);

            if (!reserved)
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                _log.LogInformation(
                    "PriceAnalyzed envelope {TransportMessageId} já na inbox; reentrega segura.",
                    envelope.MessageId);
                return ProcessOutcome.AlreadyProcessed;
            }

            if (!TryResolveRawDocumentId(evt, out var rawDocId))
            {
                _log.LogWarning(
                    "PriceAnalyzedEvent {EventId}: não foi possível resolver RawDocumentId (RawDocumentId={Raw}, ProductId={Product}).",
                    evt.EventId, evt.RawDocumentId, evt.ProductId);
                await inbox
                    .MarkCompletedAsync(ProcessingConsumerGroups.PriceAnalyzed, envelope.MessageId, ct)
                    .ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return ProcessOutcome.Skip;
            }

            var expenses = sp.GetRequiredService<IExpenseRepository>();
            var products = sp.GetRequiredService<IProductRepository>();
            var auditLogs = sp.GetRequiredService<IAuditLogRepository>();

            var expense = await expenses.GetByRawDocumentIdAsync(rawDocId, ct).ConfigureAwait(false);
            if (expense is null)
            {
                _log.LogWarning(
                    "PriceAnalyzedEvent {EventId}: nenhuma Expense para raw_document_id {RawDocumentId} no tenant {TenantId}.",
                    evt.EventId, rawDocId, tenantId);
                await inbox
                    .MarkCompletedAsync(ProcessingConsumerGroups.PriceAnalyzed, envelope.MessageId, ct)
                    .ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return ProcessOutcome.Skip;
            }

            var processingBefore = expense.ProcessingStatus;
            var hasProductSignal = evt.LastPrice > 0m && !string.IsNullOrWhiteSpace(evt.ProductId);
            var cataloguePersisted = false;

            if (hasProductSignal
                && (expense.ProcessingStatus == ExpenseProcessingStatus.Completed
                    || expense.ProcessingStatus == ExpenseProcessingStatus.PartiallyCompleted))
            {
                expense.BeginPriceBenchmark();
                try
                {
                    await products.UpsertByExternalIdAsync(
                        externalId: evt.ProductId.Trim(),
                        source: "price-analysis",
                        name: string.IsNullOrWhiteSpace(evt.ProductName) ? evt.ProductId.Trim() : evt.ProductName.Trim(),
                        price: evt.LastPrice,
                        category: string.IsNullOrWhiteSpace(evt.Category) ? null : evt.Category.Trim(),
                        ct).ConfigureAwait(false);
                    cataloguePersisted = true;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex,
                        "PriceAnalyzedEvent {EventId}: falha ao persistir Product para ExternalId={ProductId}.",
                        evt.EventId, evt.ProductId);
                }

                expense.CompletePriceBenchmark(cataloguePersisted);
            }
            else if (hasProductSignal)
            {
                _log.LogInformation(
                    "PriceAnalyzedEvent {EventId}: Expense {ExpenseId} em {Processing}; auditoria sem transição de benchmark.",
                    evt.EventId, expense.Id, expense.ProcessingStatus);
            }

            var payload = JsonSerializer.Serialize(new
            {
                evt.EventId,
                evt.ProductId,
                evt.ProductName,
                evt.Category,
                evt.Severity,
                evt.DeviationPercentage,
                evt.MarketAverage,
                evt.LastPrice,
                evt.HistoricalAverage,
                evt.AnalysisDate,
                evt.HasAnomalies,
                evt.RawDocumentId,
                processingBefore = processingBefore.ToString(),
                processingAfter = expense.ProcessingStatus.ToString(),
                cataloguePersisted
            }, JsonOpts);

            var logEntry = AuditLog.FromEntry(
                tenantId,
                entityName: nameof(Expense),
                entityId: expense.Id,
                action: "PriceAnalyzed",
                oldValue: null,
                newValue: payload,
                performedBy: null,
                performedByName: "price-analysis",
                createdAt: DateTime.UtcNow);

            await auditLogs.AppendAsync(logEntry, ct).ConfigureAwait(false);

            await inbox
                .MarkCompletedAsync(ProcessingConsumerGroups.PriceAnalyzed, envelope.MessageId, ct)
                .ConfigureAwait(false);

            await tx.CommitAsync(ct).ConfigureAwait(false);

            _log.LogInformation(
                "PriceAnalyzedEvent {EventId} → Expense {ExpenseId} rawDoc {RawDocumentId}: pipeline {ProcessingBefore}→{ProcessingAfter}.",
                evt.EventId, expense.Id, rawDocId, processingBefore, expense.ProcessingStatus);

            return ProcessOutcome.Ok;
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsValid(PriceAnalyzedEvent evt) =>
        evt is not null && evt.EventId != Guid.Empty;

    private static bool TryResolveRawDocumentId(PriceAnalyzedEvent evt, out Guid rawDocId)
    {
        rawDocId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(evt.RawDocumentId) && Guid.TryParse(evt.RawDocumentId.Trim(), out rawDocId))
            return true;

        if (string.IsNullOrWhiteSpace(evt.ProductId))
            return false;

        var sep = evt.ProductId.IndexOf(':');
        var prefix = sep > 0 ? evt.ProductId[..sep].Trim() : evt.ProductId.Trim();
        return Guid.TryParse(prefix, out rawDocId) && rawDocId != Guid.Empty;
    }

    private enum ProcessOutcome { Ok, Skip, AlreadyProcessed, Invalid }
}
