using System;

namespace Simcag.ProcessingService.Domain.Exceptions;

/// <summary>
/// Exceção lançada quando uma invariante de domínio é violada
/// (ex.: aprovar despesa sem itens, pagar acima do total).
/// Os handlers devem mapear para HTTP 422 (Unprocessable Entity).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}
