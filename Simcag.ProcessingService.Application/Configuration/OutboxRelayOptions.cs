namespace Simcag.ProcessingService.Application.Configuration;

/// <summary>Configuração do worker de relay da outbox transacional.</summary>
public sealed class OutboxRelayOptions
{
    public const string SectionName = "OutboxRelay";

    /// <summary>Intervalo entre ciclos de despacho quando há mensagens na outbox.</summary>
    public int PollIntervalMilliseconds { get; set; } = 750;

    /// <summary>Intervalo quando a outbox está vazia (evita polling agressivo ao Postgres).</summary>
    public int IdlePollIntervalMilliseconds { get; set; } = 5000;

    public int BatchSize { get; set; } = 32;

    /// <summary>Tentativas máximas por mensagem antes de poison.</summary>
    public int MaxPublishAttempts { get; set; } = 16;

    /// <summary>Bloqueio otimista ao reclamar mensagens para publicação (multi-instância).</summary>
    public int ClaimLockSeconds { get; set; } = 45;

    /// <summary>Backoff base (exponencial) após falha de publicação.</summary>
    public int RetryBackoffBaseSeconds { get; set; } = 5;
}
