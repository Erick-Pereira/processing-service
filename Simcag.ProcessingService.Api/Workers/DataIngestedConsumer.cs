using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging.Contracts;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Api.Workers;

/// <summary>
/// Consumer canônico v1 da fila <c>data-ingested-events</c>.
///
/// Para cada <see cref="DataIngestedEvent"/>:
/// 1. Cria um scope DI dedicado (DbContext + repos + AmbientPrincipal).
/// 2. Popula <see cref="AmbientPrincipal"/> com TenantId/UploadedBy do evento → o DbContext,
///    o AuditInterceptor e os global query filters passam a "ver" o tenant correto.
/// 3. Despacha <see cref="IngestExpenseFromDocumentCommand"/> via MediatR. O command é
///    idempotente por <c>RawDocumentId</c> (índice único no banco).
/// 4. Marca o EventId como processado em <see cref="IIdempotencyChecker"/> (defesa em
///    profundidade contra reentrega antes do ack chegar).
/// 5. Ack/Nack RabbitMQ com retry exponencial.
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
                var status = await ProcessAsync(evt, ct);

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

    private async Task<ProcessOutcome> ProcessAsync(DataIngestedEvent evt, CancellationToken ct)
    {
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

        // Defesa em profundidade: se a mensagem foi reentregue antes do ack,
        // o IdempotencyChecker (Redis ou ProcessedEvents table) corta na origem.
        var idem = sp.GetRequiredService<IIdempotencyChecker>();
        if (await idem.IsAlreadyProcessed(evt.EventId.ToString(), ct))
        {
            _log.LogInformation("DataIngestedEvent {EventId} já processado (idempotency check).", evt.EventId);
            return ProcessOutcome.AlreadyProcessed;
        }

        var mediator = sp.GetRequiredService<IMediator>();
        var result = await mediator.Send(new IngestExpenseFromDocumentCommand(
            RawDocumentId: evt.DocumentId,
            DocumentType: evt.DocumentType,
            Description: evt.ExtractedFields.Description,
            Amount: evt.ExtractedFields.Amount,
            IssueDate: evt.ExtractedFields.Date,
            SupplierName: evt.ExtractedFields.SupplierName,
            SupplierTaxId: evt.ExtractedFields.SupplierTaxId,
            FallbackCategory: "Outros"), ct);

        await idem.MarkAsProcessed(evt.EventId.ToString(), ct);

        _log.LogInformation(
            "DataIngestedEvent {EventId} → Expense {ExpenseId} (alreadyIngested={Already}).",
            evt.EventId, result.ExpenseId, result.AlreadyIngested);

        return result.AlreadyIngested ? ProcessOutcome.AlreadyProcessed : ProcessOutcome.Ok;
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
