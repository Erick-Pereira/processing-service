using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.Shared.Events;
using Simcag.ProcessingService.Application;
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

            const int maxRetries = 3;

            try
            {
                await foreach (var messageEnvelope in _eventConsumer.ReadMessagesAsync(stoppingToken))
                {
                    var retryCount = 0;
                    var priceEvent = messageEnvelope.Data;

                    while (retryCount < maxRetries)
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var useCase = scope.ServiceProvider.GetRequiredService<ProcessPriceCollectedEventUseCase>();

                            var result = await useCase.Handle(priceEvent, stoppingToken);

                            if (result.Status == ProcessingStatus.Success || result.Status == ProcessingStatus.AlreadyProcessed)
                            {
                                await _eventConsumer.AcknowledgeMessageAsync(messageEnvelope, stoppingToken);
                                _logger.LogInformation("✅ Evento {EventId} processado com sucesso", priceEvent.EventId);
                                break;
                            }
                            else if (result.Status == ProcessingStatus.Invalid)
                            {
                                await _eventConsumer.RejectMessageAsync(messageEnvelope, stoppingToken);
                                _logger.LogWarning("⚠️ Evento {EventId} inválido, rejeitado sem requeue", priceEvent.EventId);
                                break;
                            }
                            else if (result.Status == ProcessingStatus.Failed)
                            {
                                retryCount++;
                                if (retryCount >= maxRetries)
                                {
                                    await _eventConsumer.RejectMessageAsync(messageEnvelope, stoppingToken);
                                    _logger.LogError("❌ Falha após {RetryCount} tentativas ao processar evento {EventId}", retryCount, priceEvent.EventId);
                                    break;
                                }

                                await RetryWithBackoff(retryCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            _logger.LogError(ex, "❌ Erro inesperado ao processar evento {EventId}, tentativa {RetryCount}", priceEvent.EventId, retryCount);

                            if (retryCount >= maxRetries)
                            {
                                await _eventConsumer.RejectMessageAsync(messageEnvelope, stoppingToken);
                                break;
                            }

                            await RetryWithBackoff(retryCount);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processing Service Worker cancelado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro crítico no Processing Service Worker");
                throw;
            }
        }

        private async Task RetryWithBackoff(int retryCount)
        {
            // Exponential backoff: 1s, 4s, 9s
            var delay = TimeSpan.FromSeconds(Math.Pow(retryCount, 2));
            _logger.LogWarning("Tentativa {RetryCount} falhou, aguardando {Delay}s antes de tentar novamente", retryCount, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}