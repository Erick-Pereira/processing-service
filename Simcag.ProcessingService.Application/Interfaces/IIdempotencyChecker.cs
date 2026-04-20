using System;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Interfaces
{
    public interface IIdempotencyChecker
    {
        Task<bool> IsAlreadyProcessed(string eventId, CancellationToken cancellationToken = default);
        Task MarkAsProcessed(string eventId, CancellationToken cancellationToken = default);
    }
}
