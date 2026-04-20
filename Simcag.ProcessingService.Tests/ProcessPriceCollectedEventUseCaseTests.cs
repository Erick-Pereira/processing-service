using Microsoft.Extensions.Logging;
using Moq;
using Simcag.IngestionService.Domain.Events;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.UseCases;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Domain.Events;
using Simcag.ProcessingService.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Simcag.ProcessingService.Tests
{
    public class ProcessPriceCollectedEventUseCaseTests
    {
        [Fact]
        public async Task Handle_WithValidEvent_ShouldCreateNewProduct()
        {
            // Arrange
            var mockIdempotencyChecker = new Mock<IIdempotencyChecker>();
            var mockProductRepository = new Mock<IProductRepository>();
            var mockEventPublisher = new Mock<IEventPublisher<PriceDataProcessedEvent>>();
            var mockLogger = new Mock<ILogger<ProcessPriceCollectedEventUseCase>>();

            var useCase = new ProcessPriceCollectedEventUseCase(
                mockIdempotencyChecker.Object,
                mockProductRepository.Object,
                mockEventPublisher.Object,
                mockLogger.Object);

            var @event = new PriceCollectedEvent
            {
                EventId = Guid.NewGuid(),
                ProductId = "EXT001",
                ProductName = "Produto Teste",
                Price = 99.9m,
                Source = "Teste",
                Market = "Categoria Teste",
                OccurredAt = DateTime.UtcNow
            };

            mockIdempotencyChecker.Setup(x => x.IsAlreadyProcessed(@event.EventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            mockProductRepository.Setup(x => x.GetByExternalIdAsync(@event.ProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            var product = Product.Create(@event.ProductId, @event.ProductName, @event.Price, @event.Source, @event.Market, @event.OccurredAt);
            mockProductRepository.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            // Act
            var result = await useCase.Handle(@event, CancellationToken.None);

            // Assert
            Assert.True(result.Status == ProcessingStatus.Success);
            mockProductRepository.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
            mockEventPublisher.Verify(x => x.PublishAsync(It.IsAny<PriceDataProcessedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithAlreadyProcessedEvent_ShouldReturnAlreadyProcessed()
        {
            // Arrange
            var mockIdempotencyChecker = new Mock<IIdempotencyChecker>();
            var mockProductRepository = new Mock<IProductRepository>();
            var mockEventPublisher = new Mock<IEventPublisher<PriceDataProcessedEvent>>();
            var mockLogger = new Mock<ILogger<ProcessPriceCollectedEventUseCase>>();

            var useCase = new ProcessPriceCollectedEventUseCase(
                mockIdempotencyChecker.Object,
                mockProductRepository.Object,
                mockEventPublisher.Object,
                mockLogger.Object);

            var @event = new PriceCollectedEvent
            {
                EventId = Guid.NewGuid(),
                ProductId = "EXT001",
                ProductName = "Produto Teste",
                Price = 99.9m,
                Source = "Teste",
                Market = "Categoria Teste",
                OccurredAt = DateTime.UtcNow
            };

            mockIdempotencyChecker.Setup(x => x.IsAlreadyProcessed(@event.EventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await useCase.Handle(@event, CancellationToken.None);

            // Assert
            Assert.True(result.Status == ProcessingStatus.AlreadyProcessed);
            mockProductRepository.Verify(x => x.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            mockProductRepository.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithInvalidEvent_ShouldReturnInvalid()
        {
            // Arrange
            var mockIdempotencyChecker = new Mock<IIdempotencyChecker>();
            var mockProductRepository = new Mock<IProductRepository>();
            var mockEventPublisher = new Mock<IEventPublisher<PriceDataProcessedEvent>>();
            var mockLogger = new Mock<ILogger<ProcessPriceCollectedEventUseCase>>();

            var useCase = new ProcessPriceCollectedEventUseCase(
                mockIdempotencyChecker.Object,
                mockProductRepository.Object,
                mockEventPublisher.Object,
                mockLogger.Object);

            var @event = new PriceCollectedEvent
            {
                EventId = Guid.Empty, // Invalid event
                ProductId = "EXT001",
                ProductName = "Produto Teste",
                Price = 99.9m,
                Source = "Teste",
                Market = "Categoria Teste",
                OccurredAt = DateTime.UtcNow
            };

            // Act
            var result = await useCase.Handle(@event, CancellationToken.None);

            // Assert
            Assert.True(result.Status == ProcessingStatus.Invalid);
        }

        [Fact]
        public async Task Handle_WithValidEvent_ShouldUpdateExistingProduct()
        {
            // Arrange
            var mockIdempotencyChecker = new Mock<IIdempotencyChecker>();
            var mockProductRepository = new Mock<IProductRepository>();
            var mockEventPublisher = new Mock<IEventPublisher<PriceDataProcessedEvent>>();
            var mockLogger = new Mock<ILogger<ProcessPriceCollectedEventUseCase>>();

            var useCase = new ProcessPriceCollectedEventUseCase(
                mockIdempotencyChecker.Object,
                mockProductRepository.Object,
                mockEventPublisher.Object,
                mockLogger.Object);

            var @event = new PriceCollectedEvent
            {
                EventId = Guid.NewGuid(),
                ProductId = "EXT001",
                ProductName = "Produto Teste",
                Price = 99.9m,
                Source = "Teste",
                Market = "Categoria Teste",
                OccurredAt = DateTime.UtcNow
            };

            mockIdempotencyChecker.Setup(x => x.IsAlreadyProcessed(@event.EventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var existingProduct = Product.Create(@event.ProductId, "Produto Original", 50m, @event.Source, @event.Market, @event.OccurredAt);
            mockProductRepository.Setup(x => x.GetByExternalIdAsync(@event.ProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProduct);

            mockProductRepository.Setup(x => x.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProduct);

            // Act
            var result = await useCase.Handle(@event, CancellationToken.None);

            // Assert
            Assert.True(result.Status == ProcessingStatus.Success);
            mockProductRepository.Verify(x => x.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
            mockEventPublisher.Verify(x => x.PublishAsync(It.IsAny<PriceDataProcessedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}