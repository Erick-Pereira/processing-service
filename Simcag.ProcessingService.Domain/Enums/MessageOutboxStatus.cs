namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Estado de persistência e envio de uma mensagem na outbox transacional.</summary>
public enum MessageOutboxStatus
{
    Pending = 1,
    Dispatching = 2,
    Published = 3,
    Poisoned = 4,
}
