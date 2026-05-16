using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.Messaging;
using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging.Contracts;
using Simcag.Shared.Messaging.Telemetry;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Api.Workers;

/// <summary>
/// Consumer canônico v1 da fila <c>data-ingested-events</c>.
///
/// Para cada <see cref="DataIngestedEvent"/>:
/// 1. Cria um scope DI dedicado (DbContext + repos + AmbientPrincipal).
/// 2. Popula <see cref="AmbientPrincipal"/> com TenantId/UploadedBy do evento → o DbContext,
///    o AuditInterceptor e os global query filters passam a "ver" o tenant correto.
/// 3. Abre transação no <see cref="ProcessingDbContext"/> e reserva inbox
///    (<see cref="IConsumerInbox"/>) por <c>transport_message_id</c> (envelope) — dedupe real de reentrega.
/// 4. Despacha <see cref="IngestExpenseFromDocumentCommand"/> via MediatR. Idempotência de negócio por
///    <c>RawDocumentId</c> (índice único) continua a aplicar-se dentro da mesma transação.
/// 5. Marca inbox como concluída e faz commit — atomicidade inbox + despesa + auditoria (mesmo DbContext).
/// 6. Ack/Nack RabbitMQ com retry exponencial.
///
/// Substitui o <c>PriceProcessingBackgroundService</c> (legado, conceito "produto").
/// </summary>
public sealed class DataIngestedConsumer : BackgroundService
{
    private readonly IEventConsumer<DataIngestedEvent> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataIngestedConsumer> _log;

    private const int MaxRetries = 3;

    public DataIngestedConsumer(
        IEventConsumer<DataIngestedEvent> consumer,
        IServiceScopeFactory scopeFactory,
        ILogger<DataIngestedConsumer> log)
    {
        _consumer = consumer;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DataIngestedConsumer iniciado (fila data-ingested-events).");

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
            _log.LogInformation("DataIngestedConsumer cancelado.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erro crítico no DataIngestedConsumer; o BackgroundService irá reiniciar.");
            throw;
        }
    }

    private async Task HandleEnvelopeAsync(MessageEnvelope<DataIngestedEvent> envelope, CancellationToken ct)
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
                    case ProcessOutcome.AlreadyProcessed:
                        await _consumer.AcknowledgeMessageAsync(envelope, ct);
                        return;

                    case ProcessOutcome.Invalid:
                        _log.LogWarning("DataIngestedEvent {EventId} inválido; rejeitando sem requeue.", evt.EventId);
                        await _consumer.RejectMessageAsync(envelope, ct);
                        return;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Falha ao processar DataIngestedEvent {EventId} (tentativa {Attempt}/{Max}).",
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

    private async Task<ProcessOutcome> ProcessAsync(MessageEnvelope<DataIngestedEvent> envelope, CancellationToken ct)
    {
        var evt = envelope.Data;
        if (!IsValid(evt))
        {
            return ProcessOutcome.Invalid;
        }

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        // Popula o tenant/user ambiente desse scope antes de qualquer dispatch.
        // Sem isso, o DbContext.SaveChanges falha com "TenantId obrigatório".
        var ambient = sp.GetRequiredService<AmbientPrincipal>();
        ambient.Set(
            tenantId: evt.TenantId,
            userId: evt.UploadedBy == Guid.Empty ? null : evt.UploadedBy,
            userName: "ingestion-pipeline");

        var db = sp.GetRequiredService<ProcessingDbContext>();
        var inbox = sp.GetRequiredService<IConsumerInbox>();

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var reserved = await inbox.TryReserveAsync(
                    ProcessingConsumerGroups.DataIngested,
                    envelope.MessageId,
                    evt.TenantId,
                    evt.EventId,
                    ct)
                .ConfigureAwait(false);

            if (!reserved)
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                _log.LogInformation(
                    "DataIngested envelope {TransportMessageId} já reservado na inbox; reentrega segura.",
                    envelope.MessageId);
                return ProcessOutcome.AlreadyProcessed;
            }

            var mediator = sp.GetRequiredService<IMediator>();
        var lines = ResolveIngestedLines(evt);
        _log.LogInformation(
            "DataIngestedEvent {EventId}: linhas para despesa = {LineCount} (ExtractedFields.Lines={Direct}, fallback Extra={FromExtra}).",
            evt.EventId,
            lines?.Count ?? 0,
            evt.ExtractedFields?.Lines?.Count ?? 0,
            lines is not null && (evt.ExtractedFields?.Lines is null or { Count: 0 }));

        var ef = evt.ExtractedFields;
        var result = await mediator.Send(new IngestExpenseFromDocumentCommand(
            RawDocumentId: evt.DocumentId,
            DocumentType: evt.DocumentType,
            Description: ef?.Description,
            Amount: ef?.Amount,
            IssueDate: ef?.Date,
            SupplierName: ef?.SupplierName,
            SupplierTaxId: ef?.SupplierTaxId,
            FallbackCategory: "Outros",
            Lines: lines,
            RawText: evt.RawText), ct).ConfigureAwait(false);

            await inbox
                .MarkCompletedAsync(ProcessingConsumerGroups.DataIngested, envelope.MessageId, ct)
                .ConfigureAwait(false);

            await tx.CommitAsync(ct).ConfigureAwait(false);

            _log.LogInformation(
                "DataIngestedEvent {EventId} → Expense {ExpenseId} (alreadyIngested={Already}).",
                evt.EventId, result.ExpenseId, result.AlreadyIngested);

            return result.AlreadyIngested ? ProcessOutcome.AlreadyProcessed : ProcessOutcome.Ok;
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    private static readonly JsonSerializerOptions LinesJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Preferir <see cref="ExtractedFields.Lines"/>; se vier vazio após JSON (mensagens antigas / serialização),
    /// reidratar a partir de <c>Extra["ingestedLinesJson"]</c> preenchido na ingestão.
    /// </summary>
    private static IReadOnlyList<IngestedExpenseLine>? ResolveIngestedLines(DataIngestedEvent evt)
    {
        if (evt.ExtractedFields?.Lines is { Count: > 0 } direct)
            return direct;

        return TryDeserializeLinesFromExtra(evt.ExtractedFields?.Extra);
    }

    private static List<IngestedExpenseLine>? TryDeserializeLinesFromExtra(Dictionary<string, object?>? extra)
    {
        if (extra is null || !extra.TryGetValue("ingestedLinesJson", out var raw) || raw is null)
            return null;

        var json = raw switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            JsonElement je when je.ValueKind == JsonValueKind.Array => je.GetRawText(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<IngestedExpenseLine>>(json, LinesJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValid(DataIngestedEvent evt)
    {
        return evt is not null
            && evt.EventId != Guid.Empty
            && evt.DocumentId != Guid.Empty
            && evt.TenantId != Guid.Empty;
    }

    private enum ProcessOutcome { Ok, AlreadyProcessed, Invalid }
}
