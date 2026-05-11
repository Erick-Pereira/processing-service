using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Api.Middleware;
using Simcag.ProcessingService.Api.Workers;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.ProcessingService.Domain.Exceptions;
using Simcag.ProcessingService.Infrastructure.Auditing;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Persistence.Repositories;
using Simcag.ProcessingService.Infrastructure.Services;
using Simcag.ProcessingService.ReadModel;
using Simcag.Shared.Auditing;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging;
using Simcag.Shared.Messaging.Configuration;
using Simcag.Shared.Messaging.Extensions;
using Simcag.Shared.MultiTenancy;
using Simcag.Shared.Hosting;

DotNetEnv.Env.NoClobber().Load();
ContainerListenConfiguration.NormalizeAspNetCoreListenUrlsInContainer();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
ContainerListenConfiguration.ApplyDockerListenUrls(builder);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "SIMC-AG Processing", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cole apenas o JWT (sem 'Bearer ')."
    });
    c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var rabbitMqOptions = new RabbitMqOptions
{
    Host = Environment.GetEnvironmentVariable("RABBITMQ__HOST") ?? throw new InvalidOperationException("RABBITMQ__HOST not set"),
    Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ__PORT") ?? "5672"),
    UserName = Environment.GetEnvironmentVariable("RABBITMQ__USERNAME") ?? throw new InvalidOperationException("RABBITMQ__USERNAME not set"),
    Password = Environment.GetEnvironmentVariable("RABBITMQ__PASSWORD") ?? throw new InvalidOperationException("RABBITMQ__PASSWORD not set"),
    VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ__VIRTUALHOST") ?? "/"
};

var connectionString = $"Host={Environment.GetEnvironmentVariable("DB__HOST")};" +
                      $"Port={Environment.GetEnvironmentVariable("DB__PORT")};" +
                      $"Database={Environment.GetEnvironmentVariable("DB__NAME")};" +
                      $"Username={Environment.GetEnvironmentVariable("DB__USER")};" +
                      $"Password={Environment.GetEnvironmentVariable("DB__PASSWORD")}";

var rabbitMqHealthUri = $"amqp://{Uri.EscapeDataString(rabbitMqOptions.UserName)}:{Uri.EscapeDataString(rabbitMqOptions.Password)}@{rabbitMqOptions.Host}:{rabbitMqOptions.Port}{(rabbitMqOptions.VirtualHost == "/" ? "/" : "/" + rabbitMqOptions.VirtualHost.TrimStart('/'))}";

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL")
    .AddRabbitMQ(rabbitMqHealthUri, name: "RabbitMQ")
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

// ===== AUDITING + MULTI-TENANCY =====
builder.Services.AddHttpContextAccessor();
builder.Services.AddSimcagAuditing();
builder.Services.AddSimcagMultiTenancy();
builder.Services.AddScoped<ProcessingAuditLogSink>();
builder.Services.AddScoped<IAuditLogSink>(sp => sp.GetRequiredService<ProcessingAuditLogSink>());

builder.Services.AddDbContext<ProcessingDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
    options.AddInterceptors(interceptor);
});

// Repos write-side
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IIdempotencyChecker, IdempotencyChecker>();
builder.Services.AddScoped<Simcag.ProcessingService.Application.Interfaces.IEventPublisher,
    Simcag.ProcessingService.Application.Adapters.SharedEventPublisherAdapter>();

// Read-side (Dapper)
builder.Services.AddScoped<IDashboardQueryRepository>(sp =>
    new DashboardQueryRepository(connectionString, sp.GetRequiredService<ITenantContext>()));

// MediatR + FluentValidation
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateExpenseCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(CreateExpenseCommand).Assembly);
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddRabbitMqMessaging(rabbitMqOptions);

var eventsExchange = EventBusConstants.GetEventsExchangeName();

// === Canônico v1: ingestion → processing ===
builder.Services.AddRabbitMqEventConsumer<DataIngestedEvent>(EventBusConstants.QueueDataIngested, eventsExchange);

// === price-analysis → processing (auditoria correlacionada ao documento) ===
builder.Services.AddRabbitMqEventConsumer<PriceAnalyzedEvent>(EventBusConstants.QueuePriceAnalyzed, eventsExchange);

// ===== AUTHN/AUTHZ confiando nos headers do gateway =====
builder.Services
    .AddAuthentication(GatewayAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, GatewayAuthenticationHandler>(GatewayAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// Background workers
builder.Services.AddHostedService<DataIngestedConsumer>();
builder.Services.AddHostedService<PriceAnalyzedConsumer>();
builder.Services.AddHostedService<DashboardRefreshWorker>();

var app = builder.Build();

// Aplica migrations no boot.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ProcessingDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Falha ao aplicar migrations do ProcessingDbContext.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Exception → HTTP status mapping.
//   NotFoundException        → 404 (recurso inexistente OU invisível por query filter de tenant)
//   DomainException          → 422 (invariante de negócio violada)
//   CrossTenantWriteException → 403 (escrita cross-tenant detectada no DbContext)
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (NotFoundException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = ex.Message,
            resource = ex.Resource,
            identifier = ex.Identifier
        });
    }
    catch (DomainException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (CrossTenantWriteException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags?.Contains("live") == true,
});

app.Run();
