namespace Simcag.ProcessingService.Application;

public enum ProcessingStatus
{
    Success,
    AlreadyProcessed,
    Invalid,
    Failed
}

public readonly record struct ProcessingResult
{
    public ProcessingStatus Status { get; }
    public string Message { get; }
    public Guid? ProcessedProductId { get; }

    private ProcessingResult(ProcessingStatus status, string? message = null, Guid? processedProductId = null)
    {
        Status = status;
        Message = message ?? string.Empty;
        ProcessedProductId = processedProductId;
    }

    public static ProcessingResult Success(Guid? productId = null) => new(ProcessingStatus.Success, null, productId);
    public static ProcessingResult AlreadyProcessed() => new(ProcessingStatus.AlreadyProcessed);
    public static ProcessingResult Invalid(string message) => new(ProcessingStatus.Invalid, message);
    public static ProcessingResult Failed(string message) => new(ProcessingStatus.Failed, message);
}
