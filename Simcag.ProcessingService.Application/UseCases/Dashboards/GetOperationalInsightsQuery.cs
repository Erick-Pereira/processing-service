using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Options;
using Simcag.ProcessingService.Application.Configuration;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Application.UseCases.Dashboards;

public sealed record GetOperationalInsightsQuery(bool ForceRefresh = false) : IRequest<OperationalInsightsEnvelope>;

public sealed class GetOperationalInsightsHandler : IRequestHandler<GetOperationalInsightsQuery, OperationalInsightsEnvelope>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IMediator _mediator;
    private readonly IOperationalInsightSnapshotRepository _snapshots;
    private readonly ITenantContext _tenant;
    private readonly IOptions<InsightSnapshotOptions> _options;

    public GetOperationalInsightsHandler(
        IMediator mediator,
        IOperationalInsightSnapshotRepository snapshots,
        ITenantContext tenant,
        IOptions<InsightSnapshotOptions> options)
    {
        _mediator = mediator;
        _snapshots = snapshots;
        _tenant = tenant;
        _options = options;
    }

    public async Task<OperationalInsightsEnvelope> Handle(GetOperationalInsightsQuery request, CancellationToken ct)
    {
        var opts = _options.Value;
        var tenantId = _tenant.TenantId;
        var utcNow = DateTime.UtcNow;

        if (!request.ForceRefresh && tenantId != Guid.Empty)
        {
            var row = await _snapshots.GetLatestValidAsync(tenantId, utcNow, opts.RuleSetVersion, ct);
            if (row is not null)
            {
                var cached = JsonSerializer.Deserialize<OperationalInsightsEnvelope>(row.PayloadJson, JsonOpts);
                if (cached is not null)
                {
                    cached.ServedFrom = "snapshot";
                    cached.SnapshotId = row.Id;
                    cached.ExpiresAtUtc = row.ExpiresAtUtc;
                    cached.RuleSetVersion = row.RuleSetVersion;
                    return OperationalInsightDeterministicNarrative.Apply(cached);
                }
            }
        }

        var computed = await OperationalInsightsComputer.ComputeAsync(_mediator, ct);
        computed.RuleSetVersion = opts.RuleSetVersion;
        computed.ServedFrom = "live";
        computed = OperationalInsightDeterministicNarrative.Apply(computed);

        if (tenantId == Guid.Empty)
            return computed;

        var json = JsonSerializer.Serialize(computed, JsonOpts);
        var contextJson = (string?)null;
        var snap = OperationalInsightSnapshot.Create(
            tenantId,
            opts.RuleSetVersion,
            TimeSpan.FromMinutes(Math.Clamp(opts.TtlMinutes, 5, 24 * 60)),
            json,
            contextJson);

        await _snapshots.AddAsync(snap, ct);
        computed.SnapshotId = snap.Id;
        computed.ExpiresAtUtc = snap.ExpiresAtUtc;
        return computed;
    }
}

public sealed record GetOperationalInsightHistoryQuery(int Take = 30)
    : IRequest<IReadOnlyList<OperationalInsightSnapshotSummaryDto>>;

public sealed class GetOperationalInsightHistoryHandler
    : IRequestHandler<GetOperationalInsightHistoryQuery, IReadOnlyList<OperationalInsightSnapshotSummaryDto>>
{
    private readonly IOperationalInsightSnapshotRepository _snapshots;
    private readonly ITenantContext _tenant;
    private readonly IOptions<InsightSnapshotOptions> _options;

    public GetOperationalInsightHistoryHandler(
        IOperationalInsightSnapshotRepository snapshots,
        ITenantContext tenant,
        IOptions<InsightSnapshotOptions> options)
    {
        _snapshots = snapshots;
        _tenant = tenant;
        _options = options;
    }

    public async Task<IReadOnlyList<OperationalInsightSnapshotSummaryDto>> Handle(
        GetOperationalInsightHistoryQuery request,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Array.Empty<OperationalInsightSnapshotSummaryDto>();

        var max = _options.Value.HistoryTakeMax;
        var take = Math.Clamp(request.Take, 1, max);
        var rows = await _snapshots.ListRecentAsync(tenantId, take, ct);
        return rows
            .Select(r => new OperationalInsightSnapshotSummaryDto
            {
                Id = r.Id,
                CreatedAtUtc = r.CreatedAtUtc,
                ExpiresAtUtc = r.ExpiresAtUtc,
                RuleSetVersion = r.RuleSetVersion
            })
            .ToList();
    }
}
