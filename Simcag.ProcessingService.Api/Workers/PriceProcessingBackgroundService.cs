using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.IngestionService.Domain.Events;
using Simcag.ProcessingService.Application.UseCases;
using Simcag.Shared.Messaging.Contracts;
using Simcag.Shared.Messaging.RabbitMQ.Internal;

namespace Simcag.ProcessingService.Api.Workers
{
    public class PriceProcessingBackgroundService : BackgroundService
    {
        private readonly IEventConsumer<PriceCollectedEvent> _eventConsumer;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PriceProcessingBackgroundService> _logger;

        public PriceProcessingBackgroundService(
            IEventConsumer<PriceCollectedEvent> eventConsumer,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<PriceProcessingBackgroundService> logger)
        {
            _eventConsumer = eventConsumer;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ Processing Service Worker iniciado com sucesso");

            _eventConsumer.Subscribe<PriceCollectedEvent>(async (@event, context) =>
            {
                const int maxRetries = 3;
                var retryCount = 0;

                while (retryCount < maxRetries)
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var useCase = scope.ServiceProvider.GetRequiredService<ProcessPriceCollectedEventUseCase>();

                        var result = await useCase.Handle(@event, stoppingToken);

                        return result.Status switch
                        {
                            ProcessingStatus.Success => MessageProcessingResult.Ack,
                            ProcessingStatus.AlreadyProcessed => MessageProcessingResult.Ack,
                            ProcessingStatus.Invalid => MessageProcessingResult.NackRequeueFalse,
                            ProcessingStatus.Failed when retryCount < maxRetries - 1 => await RetryWithBackoff(retryCount),
                            ProcessingStatus.Failed => MessageProcessingResult.NackRequeueFalse, // Send to DLQ after max retries
                            _ => MessageProcessingResult.NackRequeueTrue
                        };
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount >= maxRetries)
                        {
                            _logger.LogError(ex, "❌ Falha após {RetryCount} tentativas ao processar evento {EventId}", retryCount, @event?.EventId);
                            return MessageProcessingResult.NackRequeueFalse; // Send to DLQ
                        }

                        await RetryWithBackoff(retryCount);
                    }
                }

                return MessageProcessingResult.NackRequeueFalse;
            });

            // Manter worker vivo até receber cancelamento
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            _logger.LogInformation("Processing Service Worker parado");
        }

        private async Task<MessageProcessingResult> RetryWithBackoff(int retryCount)
        {
            // Exponential backoff: 1s, 4s, 9s
            var delay = TimeSpan.FromSeconds(Math.Pow(retryCount, 2));
            _logger.LogWarning("Tentativa {RetryCount} falhou, aguardando {Delay}s antes de tentar novamente", retryCount, delay.TotalSeconds);
            await Task.Delay(delay);
            return MessageProcessingResult.NackRequeueTrue; // Requeue for retry
        }
    }
}