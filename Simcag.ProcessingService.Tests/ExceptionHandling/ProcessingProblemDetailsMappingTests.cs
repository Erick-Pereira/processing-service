using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.ErrorHandling;
using Simcag.Shared.MultiTenancy;
using Xunit;

namespace Simcag.ProcessingService.Tests.ExceptionHandling;

public sealed class ProcessingProblemDetailsMappingTests
{
    [Fact]
    public void Processing_mappings_return_404_for_NotFoundException()
    {
        var options = CreateProcessingOptions();
        var httpContext = CreateHttpContext();
        var expenseId = Guid.NewGuid();

        var problem = SimcagProblemDetailsExceptionHandler.CreateProblemDetails(
            httpContext,
            new NotFoundException("Expense", expenseId),
            options);

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource Not Found");
        problem.Extensions["resource"].Should().Be("Expense");
        problem.Extensions["identifier"].Should().Be(expenseId.ToString());
    }

    [Fact]
    public void Processing_mappings_return_422_for_DomainException()
    {
        var options = CreateProcessingOptions();
        var httpContext = CreateHttpContext();

        var problem = SimcagProblemDetailsExceptionHandler.CreateProblemDetails(
            httpContext,
            new DomainException("Despesa já aprovada."),
            options);

        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Title.Should().Be("Business Rule Violation");
        problem.Detail.Should().Be("Despesa já aprovada.");
    }

    [Fact]
    public void Processing_mappings_return_403_for_CrossTenantWriteException()
    {
        var options = CreateProcessingOptions();
        var httpContext = CreateHttpContext();
        var current = Guid.NewGuid();
        var attempted = Guid.NewGuid();

        var problem = SimcagProblemDetailsExceptionHandler.CreateProblemDetails(
            httpContext,
            new CrossTenantWriteException("Expense", current, attempted),
            options);

        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden");
    }

    [Fact]
    public void Processing_mappings_sanitize_unhandled_exception_details()
    {
        var options = CreateProcessingOptions();
        var httpContext = CreateHttpContext();

        var problem = SimcagProblemDetailsExceptionHandler.CreateProblemDetails(
            httpContext,
            new Exception("connection string: secret=value"),
            options);

        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Detail.Should().NotContain("secret=value");
    }

    private static SimcagProblemDetailsOptions CreateProcessingOptions()
    {
        var options = new SimcagProblemDetailsOptions();

        options.Map<NotFoundException>(ex => new SimcagExceptionProblemMapping(
            StatusCodes.Status404NotFound,
            "Resource Not Found",
            Extensions: new Dictionary<string, object>
            {
                ["resource"] = ex.Resource,
                ["identifier"] = ex.Identifier
            }));

        options.Map<DomainException>(_ => new SimcagExceptionProblemMapping(
            StatusCodes.Status422UnprocessableEntity,
            "Business Rule Violation"));

        options.Map<CrossTenantWriteException>(ex => new SimcagExceptionProblemMapping(
            StatusCodes.Status403Forbidden,
            "Forbidden",
            Extensions: new Dictionary<string, object>
            {
                ["entity"] = ex.Entity,
                ["currentTenant"] = ex.CurrentTenant.ToString(),
                ["attemptedTenant"] = ex.AttemptedTenant.ToString()
            }));

        return options;
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/expenses/test";
        httpContext.TraceIdentifier = "test-trace-id";
        return httpContext;
    }
}
