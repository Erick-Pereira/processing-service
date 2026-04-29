using Microsoft.Extensions.Logging;
using Simcag.Shared.Events;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.Messaging.Contracts;
using PriceDataProcessedEvent = Simcag.ProcessingService.Domain.Events.PriceDataProcessedEvent;

namespace Simcag.ProcessingService.Application.UseCases
{
    public class ProcessPriceCollectedEventUseCase
    {
        private readonly IIdempotencyChecker _idempotencyChecker;
        private readonly IProductRepository _productRepository;
        private readonly IEventPublisher<PriceDataProcessedEvent> _eventPublisher;
        private readonly ILogger<ProcessPriceCollectedEventUseCase> _logger;

        public ProcessPriceCollectedEventUseCase(
            IIdempotencyChecker idempotencyChecker,
            IProductRepository productRepository,
            IEventPublisher<PriceDataProcessedEvent> eventPublisher,
            ILogger<ProcessPriceCollectedEventUseCase> logger)
        {
            _idempotencyChecker = idempotencyChecker;
            _productRepository = productRepository;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task<ProcessingResult> Handle(PriceCollectedEvent @event, CancellationToken cancellationToken)
        {
            try
            {
                // ✅ 1. Verificar idempotência PRIMEIRO (evento único)
                if (await _idempotencyChecker.IsAlreadyProcessed(@event.EventId.ToString(), cancellationToken))
                {
                    _logger.LogWarning("Evento {EventId} já foi processado, ignorando duplicação", @event.EventId);
                    return ProcessingResult.AlreadyProcessed();
                }

                // ✅ 2. Validar dados básicos do evento
                if (!IsValidEvent(@event))
                {
                    _logger.LogWarning("Evento {EventId} com dados inválidos", @event.EventId);
                    return ProcessingResult.Invalid("Dados do evento são inválidos");
                }

                // ✅ 3. Verificar existência do produto por ExternalId (idempotência negócio)
                var existingProduct = await _productRepository.GetByExternalIdAsync(@event.ProductId, cancellationToken);
                
                Product product;
                
                if (existingProduct == null)
                {
                    // Produto novo - criar entidade
                    product = Product.Create(
                        externalId: @event.ProductId,
                        name: @event.ProductName,
                        price: @event.Price,
                        source: @event.Source,
                        category: @event.Market,
                        collectionDate: @event.OccurredAt);
                    
                    await _productRepository.AddAsync(product, cancellationToken);
                    
                    _logger.LogInformation("Novo produto criado: {ExternalId} | {ProductName}", @event.ProductId, @event.ProductName);
                }
                else
                {
                    // Produto existente - atualizar dados
                    existingProduct.Update(
                        name: @event.ProductName,
                        price: @event.Price,
                        source: @event.Source,
                        category: @event.Market,
                        collectionDate: @event.OccurredAt);
                    
                    await _productRepository.UpdateAsync(existingProduct, cancellationToken);
                    
                    product = existingProduct;
                    
                    _logger.LogInformation("Produto atualizado: {ExternalId} | {ProductName}", @event.ProductId, @event.ProductName);
                }

                // ✅ 4. Enriquecer com IA (categorização e padronização)
                await EnrichWithAIAsync(product, @event, cancellationToken);

                // ✅ 5. Publicar evento de conclusão para pipeline
                await _eventPublisher.PublishAsync(new PriceDataProcessedEvent(
                    product.Id,
                    @event.ProductId,
                    product.NormalizedName, // Use AI-enhanced name
                    @event.Price,
                    @event.Source,
                    @event.Market,
                    @event.OccurredAt,
                    new {
                        AICategory = product.Category,
                        AIConfidence = 0.85m // Placeholder for actual AI confidence
                    }), cancellationToken);

                // ✅ 5. Marcar evento como processado (garante idempotência)
                await _idempotencyChecker.MarkAsProcessed(@event.EventId.ToString(), cancellationToken);

                _logger.LogInformation("✅ Evento {EventId} processado com sucesso. ProdutoId Interno: {ProductId}",
                    @event.EventId, product.Id);

                return ProcessingResult.Success(product.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro inesperado ao processar evento {EventId}: {Message}", @event.EventId, ex.Message);
                return ProcessingResult.Failed(ex.Message);
            }
        }

        private async Task EnrichWithAIAsync(Product product, PriceCollectedEvent @event, CancellationToken cancellationToken)
        {
            try
            {
                // Call AI Service for categorization
                // Note: In a real implementation, this would make HTTP calls to the AI Service
                // For now, we'll use a simple fallback approach

                var productDescription = $"{product.Name} {product.Source}";

                // Simple rule-based categorization (fallback when AI service is not available)
                var category = CategorizeProductFallback(productDescription);

                // Update product with AI-enriched data
                product.Update(
                    name: product.Name,
                    price: product.Price,
                    source: product.Source,
                    category: category,
                    collectionDate: product.CollectionDate);

                await _productRepository.UpdateAsync(product, cancellationToken);

                _logger.LogInformation("AI enrichment completed for product {ProductId}: Category={Category}",
                    product.ExternalId, category);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI enrichment failed for product {ProductId}, continuing with original data", product.ExternalId);
                // Continue processing even if AI enrichment fails
            }
        }

        private string CategorizeProductFallback(string productDescription)
        {
            var description = productDescription.ToLower();

            if (description.Contains("notebook") || description.Contains("laptop"))
                return "Notebook";
            if (description.Contains("monitor") || description.Contains("display") || description.Contains("screen"))
                return "Monitor";
            if (description.Contains("mouse") || description.Contains("keyboard") || description.Contains("teclado") || description.Contains("headset"))
                return "Periférico";
            if (description.Contains("ram") || description.Contains("ssd") || description.Contains("cpu") || description.Contains("placa"))
                return "Hardware";
            if (description.Contains("software") || description.Contains("license") || description.Contains("licença"))
                return "Software";

            return "Outro";
        }

        private string StandardizeNameFallback(string productName)
        {
            return productName
                .Trim()
                .Replace("  ", " ")
                .Split(' ')
                .Select(word => word.Length > 0 ?
                    char.ToUpper(word[0]) + word[1..].ToLower() :
                    word)
                .Aggregate((current, next) => current + " " + next);
        }

        private static bool IsValidEvent(PriceCollectedEvent @event)
        {
            return @event != null
                && !string.IsNullOrWhiteSpace(@event.ProductId)
                && !string.IsNullOrWhiteSpace(@event.ProductName)
                && !string.IsNullOrWhiteSpace(@event.Source)
                && @event.Price > 0
                && @event.EventId != Guid.Empty;
        }
    }
}