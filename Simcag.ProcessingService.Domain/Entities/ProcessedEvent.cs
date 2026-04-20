using Simcag.Shared.Common;

namespace Simcag.ProcessingService.Domain.Entities;

public class ProcessedEvent : BaseEntity
{
    public Guid EventId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private ProcessedEvent() { } // EF Core

    public static ProcessedEvent Create(Guid eventId)
    {
        return new ProcessedEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ProcessedAt = DateTime.UtcNow
        };
    }
}