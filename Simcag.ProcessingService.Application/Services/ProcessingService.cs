using Microsoft.Extensions.Logging;
using Simcag.IngestionService.Domain.Events;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Events;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Services
{
    public class ProcessingService : IProcessingService
    {
        private readonly IProductRepository _productRepository;
        private readonly IIdempotencyChecker _idempotencyChecker;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<ProcessingService> _logger;

        public ProcessingService(
            IProductRepository productRepository,
            IIdempotencyChecker idempotencyChecker,
            IEventPublisher eventPublisher,
            ILogger<ProcessingService> logger)
        {
            _productRepository = productRepository;
            _idempotencyChecker = idempotencyChecker;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task ProcessPriceCollectedEventAsync(PriceCollectedEvent priceEvent, CancellationToken cancellationToken = default)
        {
            using var scope = _logger.BeginScope("{EventId} {ProductId}", priceEvent.EventId, priceEvent.ProductId);

            if (await _idempotencyChecker.IsAlreadyProcessed(priceEvent.EventId.ToString(), cancellationToken))
            {
                _logger.LogInformation("Event already processed, skipping");
                return;
            }

            _logger.LogInformation("Starting processing of price collected event");

            try
            {
                var existingProduct = await _productRepository.GetByExternalIdAsync(priceEvent.ProductId, cancellationToken);

                Product product;

                if (existingProduct is null)
                {
                    _logger.LogInformation("Creating new product");
                    product = Product.Create(
                        priceEvent.ProductId,
                        priceEvent.ProductName,
                        priceEvent.Price,
                        priceEvent.Source,
                        priceEvent.Market, // Usando Market do evento
                        priceEvent.OccurredAt); // Usando OccurredAt

                    await _productRepository.AddAsync(product, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Updating existing product");
                    existingProduct.Update(
                        priceEvent.ProductName,
                        priceEvent.Price,
                        priceEvent.Source,
                        priceEvent.Market, // Usando Market do evento
                        priceEvent.OccurredAt); // Usando OccurredAt

                    await _productRepository.UpdateAsync(existingProduct, cancellationToken);
                    product = existingProduct;
                }

                await _idempotencyChecker.MarkAsProcessed(priceEvent.EventId.ToString(), cancellationToken);

                var processedEvent = new PriceDataProcessedEvent(
                    product.Id, // Product ID as Guid
                    priceEvent.ProductId,
                    priceEvent.ProductName,
                    priceEvent.Price,
                    priceEvent.Source,
                    priceEvent.Market,
                    priceEvent.OccurredAt,
                    new
                    {
                        ProductId = priceEvent.ProductId,
                        ProcessedProductId = product.Id,
                        NormalizedName = product.NormalizedName,
                        Price = product.Price
                    });

                await _eventPublisher.PublishAsync(processedEvent, cancellationToken);

                _logger.LogInformation("Event processed successfully, ProductId: {ProductId}", product.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process event");
                throw;
            }
        }
    }
}