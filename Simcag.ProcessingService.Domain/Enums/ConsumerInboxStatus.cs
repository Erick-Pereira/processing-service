namespace Simcag.ProcessingService.Domain.Enums;

/// <summary>Estado de processamento deduplicado de uma mensagem transportada (Rabbit).</summary>
public enum ConsumerInboxStatus
{
    Pending = 1,
    Completed = 2,
}
