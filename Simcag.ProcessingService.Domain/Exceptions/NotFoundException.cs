using System;

namespace Simcag.ProcessingService.Domain.Exceptions;

/// <summary>
/// Lançada quando um recurso não existe (ou não é visível para o tenant atual,
/// graças aos global query filters — não vazamos a existência de dados de outros tenants).
/// Os handlers devem mapear para HTTP 404 (Not Found).
///
/// Diferente de <see cref="DomainException"/>, que sinaliza violação de invariante (422).
/// </summary>
public sealed class NotFoundException : Exception
{
    public string Resource { get; }
    public string Identifier { get; }

    public NotFoundException(string resource, object identifier)
        : base($"{resource} '{identifier}' não encontrado.")
    {
        Resource = resource;
        Identifier = identifier?.ToString() ?? string.Empty;
    }

    public NotFoundException(string resource, object identifier, string message)
        : base(message)
    {
        Resource = resource;
        Identifier = identifier?.ToString() ?? string.Empty;
    }
}
