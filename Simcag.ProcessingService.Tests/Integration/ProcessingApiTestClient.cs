using Microsoft.AspNetCore.Mvc.Testing;
using Simcag.Shared.Security;

namespace Simcag.ProcessingService.Tests.Integration;

internal static class ProcessingApiTestClient
{
    public static HttpClient CreateAuthenticatedClient(this ProcessingApiTestFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.ApplyGatewayAuthHeaders();

        return client;
    }
}
