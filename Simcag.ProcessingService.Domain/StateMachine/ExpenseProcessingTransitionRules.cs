using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Domain.StateMachine;

/// <summary>Transições válidas de <see cref="ExpenseProcessingStatus"/> (guardas centralizadas).</summary>
public static class ExpenseProcessingTransitionRules
{
    /// <summary>Verifica se a transição é permitida (sem efeitos colaterais).</summary>
    public static bool IsAllowed(ExpenseProcessingStatus from, ExpenseProcessingStatus to)
    {
        if (from == to)
            return true;

        return (from, to) switch
        {
            // Arranque / ingestão
            (ExpenseProcessingStatus.Received, ExpenseProcessingStatus.Enriching) => true,
            (ExpenseProcessingStatus.Received, ExpenseProcessingStatus.Persisting) => true,
            (ExpenseProcessingStatus.Received, ExpenseProcessingStatus.Completed) => true,
            (ExpenseProcessingStatus.Received, ExpenseProcessingStatus.Failed) => true,

            (ExpenseProcessingStatus.Enriching, ExpenseProcessingStatus.Benchmarking) => true,
            (ExpenseProcessingStatus.Enriching, ExpenseProcessingStatus.Persisting) => true,
            (ExpenseProcessingStatus.Enriching, ExpenseProcessingStatus.Completed) => true,
            (ExpenseProcessingStatus.Enriching, ExpenseProcessingStatus.PartiallyCompleted) => true,
            (ExpenseProcessingStatus.Enriching, ExpenseProcessingStatus.Failed) => true,

            (ExpenseProcessingStatus.Persisting, ExpenseProcessingStatus.Completed) => true,
            (ExpenseProcessingStatus.Persisting, ExpenseProcessingStatus.PartiallyCompleted) => true,
            (ExpenseProcessingStatus.Persisting, ExpenseProcessingStatus.Failed) => true,

            (ExpenseProcessingStatus.Benchmarking, ExpenseProcessingStatus.Completed) => true,
            (ExpenseProcessingStatus.Benchmarking, ExpenseProcessingStatus.PartiallyCompleted) => true,
            (ExpenseProcessingStatus.Benchmarking, ExpenseProcessingStatus.Failed) => true,

            // Reentrada de benchmark após pipeline concluída (ex.: novo PriceAnalyzed)
            (ExpenseProcessingStatus.Completed, ExpenseProcessingStatus.Benchmarking) => true,
            (ExpenseProcessingStatus.PartiallyCompleted, ExpenseProcessingStatus.Benchmarking) => true,

            // Consolidação pós-parcial
            (ExpenseProcessingStatus.PartiallyCompleted, ExpenseProcessingStatus.Completed) => true,
            (ExpenseProcessingStatus.PartiallyCompleted, ExpenseProcessingStatus.Failed) => true,

            // Retry operacional
            (ExpenseProcessingStatus.Failed, ExpenseProcessingStatus.Received) => true,

            _ => false,
        };
    }

    public static string DescribeDisallowed(ExpenseProcessingStatus from, ExpenseProcessingStatus to) =>
        $"Transição de processamento inválida: {from} → {to}.";
}
