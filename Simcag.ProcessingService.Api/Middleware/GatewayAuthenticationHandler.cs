using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Simcag.ProcessingService.Api.Middleware;

/// <summary>
/// AuthenticationHandler que confia nos headers <c>X-Tenant-Id</c>, <c>X-User-Id</c>,
/// <c>X-User-Role</c> e <c>X-User-Name</c> propagados pelo gateway (que já validou o JWT).
/// Permite uso de <c>[Authorize]</c> e <c>[Authorize(Roles="Admin")]</c> nos controllers
/// sem que o serviço downstream precise re-validar o JWT.
/// Em produção, recomenda-se que esta porta NÃO seja exposta diretamente fora do cluster.
/// </summary>
public sealed class GatewayAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Gateway";

    public GatewayAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers;
        var userId = headers["X-User-Id"].FirstOrDefault();
        var tenantId = headers["X-Tenant-Id"].FirstOrDefault();
        var role = headers["X-User-Role"].FirstOrDefault();
        var name = headers["X-User-Name"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };

        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim("tenant_id", tenantId));
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
