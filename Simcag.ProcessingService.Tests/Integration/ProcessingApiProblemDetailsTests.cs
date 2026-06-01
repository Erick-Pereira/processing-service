using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Simcag.ProcessingService.Tests.Integration;

public sealed class ProcessingApiProblemDetailsTests : IClassFixture<ProcessingApiTestFactory>
{
    private readonly ProcessingApiTestFactory _factory;

    public ProcessingApiProblemDetailsTests(ProcessingApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_NonExistentExpense_Returns_Rfc7807_404()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/api/expenses/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("title").GetString().Should().Be("Resource Not Found");
        body.TryGetProperty("resource", out var resource).Should().BeTrue();
        resource.GetString().Should().Be("Expense");
    }
}
