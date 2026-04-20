using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.ProcessingService.Infrastructure.Persistence;

namespace Simcag.ProcessingService.Infrastructure.Services
{
    public class IdempotencyChecker : IIdempotencyChecker
    {
        private readonly ProcessingDbContext _dbContext;

        public IdempotencyChecker(ProcessingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> IsAlreadyProcessed(string eventId, CancellationToken cancellationToken)
        {
            var guid = Guid.Parse(eventId);
            return await _dbContext.ProcessedEvents
                .AsNoTracking()
                .AnyAsync(x => x.EventId == guid, cancellationToken);
        }

        public async Task MarkAsProcessed(string eventId, CancellationToken cancellationToken)
        {
            var guid = Guid.Parse(eventId);
            var processedEvent = ProcessedEvent.Create(guid);
            await _dbContext.ProcessedEvents.AddAsync(processedEvent, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
