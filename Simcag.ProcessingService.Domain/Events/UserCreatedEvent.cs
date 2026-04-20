using System;

using Simcag.Shared.Events;

namespace Simcag.ProcessingService.Domain.Events;

/// <summary>
/// Evento disparado quando um usuário é criado (consumido pelo processing-service)
/// </summary>
public class UserCreatedEvent : BaseEvent
{
    /// <summary>
    /// Identificador do usuário
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Nome do usuário
    /// </summary>
    public string UserName { get; init; }

    /// <summary>
    /// Email do usuário
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Tipo do evento
    /// </summary>
    public override string EventType => "UserCreated";
}