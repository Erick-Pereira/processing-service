namespace Simcag.ProcessingService.Application.Interfaces;

/// <summary>
/// Reserva de inbox transacional: <c>TryReserveAsync</c> deve ser chamado dentro de
/// uma transação explícita do mesmo <c>DbContext</c> que persiste o efeito de negócio.
/// </summary>
public interface IConsumerInbox
{
    /// <summary>
    /// Insere reserva idempotente. Devolve <see langword="true"/> se esta entrega deve prosseguir,
    /// <see langword="false"/> se a mensagem já foi vista (reentrega segura → ack sem efeito).
    /// </summary>
    Task<bool> TryReserveAsync(
        string consumerGroup,
        Guid transportMessageId,
        Guid tenantId,
        Guid? domainEventId,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        string consumerGroup,
        Guid transportMessageId,
        CancellationToken cancellationToken = default);
}
