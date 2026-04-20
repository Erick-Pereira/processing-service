using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.Services;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.Events;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Simcag.ProcessingService.Tests;

public class EndToEndEventFlowTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IIdempotencyChecker> _idempotencyCheckerMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<ILogger<ProcessingService>> _loggerMock;
    private readonly ProcessingService _processingService;

    public EndToEndEventFlowTests()
    {
        _productRepositoryMock = new Mock<IProductRepository>();
        _idempotencyCheckerMock = new Mock<IIdempotencyChecker>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _loggerMock = new Mock<ILogger<ProcessingService>>();

        _processingService = new ProcessingService(
            _productRepositoryMock.Object,
            _idempotencyCheckerMock.Object,
            _eventPublisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CompleteProcessingFlow_ShouldCreateProductAndPublishEvent()
    {
        // Arrange
        var priceCollectedEvent = new PriceCollectedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = "e2e-test-product",
            ProductName = "Test Product Name",
            Price = 99.99m,
            Source = "test-source",
            Market = "test-market",
            OccurredAt = DateTime.UtcNow
        };

        // Setup mocks
        _idempotencyCheckerMock
            .Setup(x => x.IsAlreadyProcessed(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _productRepositoryMock
            .Setup(x => x.GetByExternalIdAsync("e2e-test-product", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null); // No existing product

        var createdProduct = Product.Create(
            "e2e-test-product",
            "Test Product Name",
            99.99m,
            "test-source",
            "test-market",
            DateTime.UtcNow);

        _productRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _processingService.ProcessPriceCollectedEventAsync(priceCollectedEvent, CancellationToken.None);

        // Assert
        // Verify product was created
        _productRepositoryMock.Verify(x => x.AddAsync(
            It.Is<Product>(p =>
                p.ExternalId == "e2e-test-product" &&
                p.Name == "Test Product Name" &&
                p.Price == 99.99m &&
                p.Source == "test-source" &&
                p.Market == "test-market"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify idempotency was marked
        _idempotencyCheckerMock.Verify(x => x.MarkAsProcessed(
            priceCollectedEvent.EventId.ToString(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify event was published
        _eventPublisherMock.Verify(x => x.PublishAsync(
            It.Is<PriceDataProcessedEvent>(e =>
                e.ProductId == "e2e-test-product" &&
                e.ProductName == "Test Product Name" &&
                e.Price == 99.99m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessingFlow_WithExistingProduct_ShouldUpdateProduct()
    {
        // Arrange
        var priceCollectedEvent = new PriceCollectedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = "existing-product",
            ProductName = "Updated Product Name",
            Price = 149.99m,
            Source = "updated-source",
            Market = "updated-market",
            OccurredAt = DateTime.UtcNow
        };

        var existingProduct = Product.Create(
            "existing-product",
            "Original Name",
            99.99m,
            "original-source",
            "original-market",
            DateTime.UtcNow.AddDays(-1));

        // Setup mocks
        _idempotencyCheckerMock
            .Setup(x => x.IsAlreadyProcessed(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _productRepositoryMock
            .Setup(x => x.GetByExternalIdAsync("existing-product", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProduct);

        _productRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _processingService.ProcessPriceCollectedEventAsync(priceCollectedEvent, CancellationToken.None);

        // Assert
        // Verify existing product was updated, not a new one created
        _productRepositoryMock.Verify(x => x.AddAsync(
            It.IsAny<Product>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _productRepositoryMock.Verify(x => x.UpdateAsync(
            It.Is<Product>(p =>
                p.ExternalId == "existing-product" &&
                p.Name == "Updated Product Name" &&
                p.Price == 149.99m &&
                p.Source == "updated-source"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify event was published
        _eventPublisherMock.Verify(x => x.PublishAsync(
            It.IsAny<PriceDataProcessedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessingFlow_WithDuplicateEvent_ShouldSkipProcessing()
    {
        // Arrange
        var priceCollectedEvent = new PriceCollectedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = "duplicate-product",
            ProductName = "Test Product",
            Price = 99.99m,
            Source = "test-source",
            Market = "test-market",
            OccurredAt = DateTime.UtcNow
        };

        // Setup mocks - event already processed
        _idempotencyCheckerMock
            .Setup(x => x.IsAlreadyProcessed(priceCollectedEvent.EventId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _processingService.ProcessPriceCollectedEventAsync(priceCollectedEvent, CancellationToken.None);

        // Assert
        // Verify no database operations occurred
        _productRepositoryMock.Verify(x => x.GetByExternalIdAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _productRepositoryMock.Verify(x => x.AddAsync(
            It.IsAny<Product>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _productRepositoryMock.Verify(x => x.UpdateAsync(
            It.IsAny<Product>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify no event was published
        _eventPublisherMock.Verify(x => x.PublishAsync(
            It.IsAny<PriceDataProcessedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify idempotency checker was not called to mark as processed again
        _idempotencyCheckerMock.Verify(x => x.MarkAsProcessed(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}