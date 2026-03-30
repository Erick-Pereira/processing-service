using Microsoft.Extensions.Logging;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using shared.Events;
using System.Globalization;

namespace Simcag.ProcessingService.Application.Services;

public class ProcessingServiceImpl : IProcessingService
{
    private readonly IProductRepository _productRepository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<ProcessingServiceImpl> _logger;

    public ProcessingServiceImpl(
        IProductRepository productRepository,
        IMessagePublisher messagePublisher,
        ILogger<ProcessingServiceImpl> logger)
    {
        _productRepository = productRepository;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task ProcessPriceEventAsync(PriceCollectedEvent priceEvent)
    {
        try
        {
            _logger.LogInformation("Processing price event for ProductId: {ProductId}", priceEvent.ProductId);

            var normalizedName = NormalizeProductName(priceEvent.ProductName);

            var product = new Product
            {
                Id = priceEvent.ProductId,
                Name = priceEvent.ProductName,
                NormalizedName = normalizedName,
                Price = priceEvent.Price,
                CollectionDate = priceEvent.CollectionDate,
                Source = priceEvent.Source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();

            _logger.LogInformation("Product saved to database with Id: {ProductId}", product.Id);

            var processedEvent = new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                NormalizedName = product.NormalizedName,
                Price = product.Price,
                CollectionDate = product.CollectionDate,
                Source = product.Source,
                ProcessedAt = DateTime.UtcNow
            };

            await _messagePublisher.PublishAsync("data.processed", processedEvent);

            _logger.LogInformation("Published data.processed event for ProductId: {ProductId}", product.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing price event for ProductId: {ProductId}", priceEvent.ProductId);
            throw;
        }
    }

    private static string NormalizeProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return string.Empty;

        // Remove extra spaces and trim
        var normalized = string.Join(" ", productName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        // Convert to Title Case
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        normalized = textInfo.ToTitleCase(normalized.ToLower());

        return normalized;
    }
}
