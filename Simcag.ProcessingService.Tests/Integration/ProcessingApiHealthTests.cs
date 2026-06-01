using System.Net;
using FluentAssertions;
using Xunit;

namespace Simcag.ProcessingService.Tests.Integration;

public sealed class ProcessingApiHealthTests : IClassFixture<ProcessingApiTestFactory>
{
    private readonly ProcessingApiTestFactory _factory;

    public ProcessingApiHealthTests(ProcessingApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_HealthLive_Returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_HealthReady_Returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
