namespace Simcag.ProcessingService.Application.Messaging;

/// <summary>Identificadores estáveis de consumidor para inbox deduplicado.</summary>
public static class ProcessingConsumerGroups
{
    public const string DataIngested = "processing.data-ingested.v1";
    public const string PriceAnalyzed = "processing.price-analyzed.v1";
}
