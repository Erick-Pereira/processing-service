using Microsoft.Extensions.DependencyInjection;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.Shared.ErrorHandling;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Api.ExceptionHandling;

public static class ProcessingProblemDetailsExtensions
{
    public static IServiceCollection AddProcessingProblemDetails(this IServiceCollection services) =>
        services.AddSimcagProblemDetails(options =>
        {
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
        });
}
