namespace Simcag.ProcessingService.Application.Configuration;

/// <summary>Configuração do worker de relay da outbox transacional.</summary>
public sealed class OutboxRelayOptions
{
    public const string SectionName = "OutboxRelay";

    /// <summary>Intervalo entre ciclos de despacho.</summary>
    public int PollIntervalMilliseconds { get; set; } = 750;

    public int BatchSize { get; set; } = 32;

    /// <summary>Tentativas máximas por mensagem antes de poison.</summary>
    public int MaxPublishAttempts { get; set; } = 16;

    /// <summary>Bloqueio otimista ao reclamar mensagens para publicação (multi-instância).</summary>
    public int ClaimLockSeconds { get; set; } = 45;

    /// <summary>Backoff base (exponencial) após falha de publicação.</summary>
    public int RetryBackoffBaseSeconds { get; set; } = 5;
}
