using Simcag.Shared.Events;
using Simcag.Shared.Messaging;
using System;

namespace Simcag.ProcessingService.Domain.Events
{
    /// <summary>
    /// Published when price data is processed.
    /// Subscribers: Analytics service, CRM sync, etc.
    /// </summary>
    public class PriceDataProcessedEvent : BaseEvent
    {
        // === Domain-Specific Properties ===
        public string Id { get; init; } = string.Empty;

        public string ProductId { get; init; } = string.Empty;

        public string ProductName { get; init; } = string.Empty;

        public decimal Price { get; init; }

        public string Source { get; init; } = string.Empty;

        public string Market { get; init; } = string.Empty;

        public DateTime Timestamp { get; init; }

        public object EventData { get; init; }

        // === Override EventType ===
        public override string EventType => EventNames.PriceDataProcessed;

        public PriceDataProcessedEvent(Guid id, string productId, string productName, decimal price, string source, string market, DateTime timestamp, object eventData)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Id cannot be empty", nameof(id));

            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId is required", nameof(productId));

            if (string.IsNullOrWhiteSpace(productName))
                throw new ArgumentException("ProductName is required", nameof(productName));

            if (price <= 0)
                throw new ArgumentException("Price must be greater than zero", nameof(price));

            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source is required", nameof(source));

            // Initialize
            Id = id.ToString();
            ProductId = productId;
            ProductName = productName;
            Price = price;
            Source = source;
            Market = market ?? string.Empty;
            Timestamp = timestamp;
            EventData = eventData;
        }

        public PriceDataProcessedEvent()
        {
        }
    }
}