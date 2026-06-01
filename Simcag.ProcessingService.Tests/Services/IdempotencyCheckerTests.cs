using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Services;
using Xunit;

namespace Simcag.ProcessingService.Tests.Services;

public sealed class IdempotencyCheckerTests
{
    [Fact]
    public async Task MarkAsProcessed_makes_IsAlreadyProcessed_return_true()
    {
        var eventId = Guid.NewGuid().ToString();
        await using var db = CreateDbContext();
        var checker = new IdempotencyChecker(db);

        (await checker.IsAlreadyProcessed(eventId, CancellationToken.None)).Should().BeFalse();

        await checker.MarkAsProcessed(eventId, CancellationToken.None);

        (await checker.IsAlreadyProcessed(eventId, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task IsAlreadyProcessed_returns_false_for_unknown_event()
    {
        await using var db = CreateDbContext();
        var checker = new IdempotencyChecker(db);

        (await checker.IsAlreadyProcessed(Guid.NewGuid().ToString(), CancellationToken.None)).Should().BeFalse();
    }

    private static ProcessingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessingDbContext>()
            .UseInMemoryDatabase($"processing-idempotency-{Guid.NewGuid()}")
            .Options;
        return new ProcessingDbContext(options);
    }
}
