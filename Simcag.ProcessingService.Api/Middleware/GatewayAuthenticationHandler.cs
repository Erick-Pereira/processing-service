using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simcag.Shared.Security;
using Simcag.Shared.Telemetry;

namespace Simcag.ProcessingService.Api.Middleware;

/// <summary>
/// AuthenticationHandler que confia nos headers propagados pelo gateway após validar o JWT
/// (<see cref="GatewayForwardedAuthHeaders"/>).
/// Permite uso de <c>[Authorize]</c> e <c>[Authorize(Roles = SimcagRoles.Admin)]</c> nos controllers
/// sem que o serviço downstream precise re-validar o JWT.
/// Com <see cref="GatewayTrustOptions.DownstreamHmacSecret"/> + prova HMAC, reduz spoofing de headers.
/// Em produção, recomenda-se que esta porta NÃO seja exposta diretamente fora do cluster.
/// </summary>
public sealed class GatewayAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Gateway";

    private readonly IOptionsMonitor<GatewayTrustOptions> _trustOptions;

    public GatewayAuthenticationHandler(
        IOptionsMonitor<GatewayTrustOptions> trustOptions,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _trustOptions = trustOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers;
        var userId = headers[GatewayForwardedAuthHeaders.UserId].FirstOrDefault();
        var tenantId = headers[GatewayForwardedAuthHeaders.TenantId].FirstOrDefault();
        var role = headers[GatewayForwardedAuthHeaders.UserRole].FirstOrDefault();
        var name = headers[GatewayForwardedAuthHeaders.UserName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!GatewayDownstreamHmac.TryValidateProof(headers, _trustOptions.CurrentValue, out var proofReason))
        {
            SimcagMeters.SecurityGatewayProofFailures.Add(1,
                new KeyValuePair<string, object?>("reason", proofReason ?? "unknown"));
            Logger.LogWarning("Gateway proof validation failed: {Reason}", proofReason);
            return Task.FromResult(AuthenticateResult.Fail($"gateway_proof:{proofReason}"));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };

        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim(SimcagClaims.TenantId, tenantId));
        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
            claims.Add(new Claim("name", name));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
