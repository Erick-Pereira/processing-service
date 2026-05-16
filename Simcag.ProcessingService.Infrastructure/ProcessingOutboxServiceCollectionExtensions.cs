using Microsoft.Extensions.DependencyInjection;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Infrastructure.Messaging;

namespace Simcag.ProcessingService.Infrastructure;

/// <summary>Registo DI da outbox transacional, inbox e relay (tipos internos ao Infrastructure).</summary>
public static class ProcessingOutboxServiceCollectionExtensions
{
    public static IServiceCollection AddProcessingTransactionalOutbox(this IServiceCollection services)
    {
        services.AddScoped<IConsumerInbox, ConsumerInboxService>();
        services.AddScoped<ITransactionalOutbox, TransactionalOutboxService>();
        services.AddScoped<OutboxRelayDispatcher>();
        services.AddScoped<OutboxRelayRunner>();
        services.AddScoped<IEventPublisher, TransactionalOutboxEventPublisherAdapter>();
        return services;
    }
}
