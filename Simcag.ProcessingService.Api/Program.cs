using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Api.ExceptionHandling;
using Simcag.ProcessingService.Api.Workers;
using Simcag.ProcessingService.Application.Configuration;
using Simcag.ProcessingService.Application.Dashboard;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.ProcessingService.Infrastructure;
using Simcag.ProcessingService.Infrastructure.Auditing;
using Simcag.ProcessingService.Infrastructure.Messaging;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Persistence.Repositories;
using Simcag.ProcessingService.Infrastructure.Services;
using Simcag.ProcessingService.ReadModel;
using Simcag.Shared.Auditing;
using Simcag.Shared.ErrorHandling;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging;
using Simcag.Shared.Messaging.Configuration;
using Simcag.Shared.Messaging.Extensions;
using Simcag.Shared.MultiTenancy;
using Simcag.Shared.Hosting;
using Simcag.Shared.Security;
using Simcag.Shared.Telemetry;

DotNetEnv.Env.NoClobber().Load();
ContainerListenConfiguration.NormalizeAspNetCoreListenUrlsInContainer();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.AddSimcagDistributedTelemetry("Simcag.ProcessingService");
ContainerListenConfiguration.ApplyDockerListenUrls(builder);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Econdomiza - Processing", Version = "v1" });
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

RabbitMqOptions rabbitMqOptions;
string connectionString;

if (isTesting)
{
    rabbitMqOptions = new RabbitMqOptions
    {
        Host = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
        VirtualHost = "/"
    };
    connectionString = "Host=localhost;Port=5432;Database=processing_test;Username=postgres;Password=postgres";
}
else
{
    rabbitMqOptions = new RabbitMqOptions
    {
        Host = Environment.GetEnvironmentVariable("RABBITMQ__HOST") ?? throw new InvalidOperationException("RABBITMQ__HOST not set"),
        Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ__PORT") ?? "5672"),
        UserName = Environment.GetEnvironmentVariable("RABBITMQ__USERNAME") ?? throw new InvalidOperationException("RABBITMQ__USERNAME not set"),
        Password = Environment.GetEnvironmentVariable("RABBITMQ__PASSWORD") ?? throw new InvalidOperationException("RABBITMQ__PASSWORD not set"),
        VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ__VIRTUALHOST") ?? "/"
    };
    rabbitMqOptions.ApplyMessageSigningFromEnvironment();

    connectionString = $"Host={Environment.GetEnvironmentVariable("DB__HOST")};" +
                       $"Port={Environment.GetEnvironmentVariable("DB__PORT")};" +
                       $"Database={Environment.GetEnvironmentVariable("DB__NAME")};" +
                       $"Username={Environment.GetEnvironmentVariable("DB__USER")};" +
                       $"Password={Environment.GetEnvironmentVariable("DB__PASSWORD")}";
}

var rabbitMqHealthUri =
    $"amqp://{Uri.EscapeDataString(rabbitMqOptions.UserName)}:{Uri.EscapeDataString(rabbitMqOptions.Password)}@{rabbitMqOptions.Host}:{rabbitMqOptions.Port}{(rabbitMqOptions.VirtualHost == "/" ? "/" : "/" + rabbitMqOptions.VirtualHost.TrimStart('/'))}";

var healthChecks = builder.Services.AddHealthChecks().AddSimcagLiveSelfCheck();

if (isTesting)
{
    healthChecks.AddCheck("database", () => HealthCheckResult.Healthy(), tags: [SimcagHealthCheckExtensions.ReadyTag]);
}
else
{
    healthChecks
        .AddNpgSql(connectionString, name: "PostgreSQL", tags: [SimcagHealthCheckExtensions.ReadyTag])
        .AddRabbitMQ(rabbitMqHealthUri, name: "RabbitMQ", tags: [SimcagHealthCheckExtensions.ReadyTag]);
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSimcagAuditing();
builder.Services.AddSimcagMultiTenancy();
builder.Services.AddScoped<ProcessingAuditLogSink>();
builder.Services.AddScoped<IAuditLogSink>(sp => sp.GetRequiredService<ProcessingAuditLogSink>());

builder.Services.AddDbContext<ProcessingDbContext>((sp, options) =>
{
    if (isTesting)
        options.UseInMemoryDatabase("processing_testing");
    else
        options.UseNpgsql(connectionString);

    var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
    options.AddInterceptors(interceptor);
});

builder.Services.Configure<InsightSnapshotOptions>(
    builder.Configuration.GetSection(InsightSnapshotOptions.SectionName));

builder.Services.Configure<OutboxRelayOptions>(
    builder.Configuration.GetSection(OutboxRelayOptions.SectionName));

builder.Services.AddSimcagGatewayAuthentication(builder.Environment);

builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IOperationalInsightSnapshotRepository, OperationalInsightSnapshotRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductCatalogQueryRepository, ProductCatalogQueryRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IExpenseComplianceRepository, ExpenseComplianceRepository>();
builder.Services.AddScoped<IIdempotencyChecker, IdempotencyChecker>();
builder.Services.AddProcessingTransactionalOutbox();

builder.Services.AddScoped<IDashboardQueryRepository>(sp =>
    new DashboardQueryRepository(connectionString, sp.GetRequiredService<ITenantContext>()));
builder.Services.AddScoped<IDashboardReadModelRefresher, DashboardReadModelRefresher>();
builder.Services.AddScoped<ISupplierQualityReadModel>(sp =>
    new SupplierQualityReadRepository(connectionString, sp.GetRequiredService<ITenantContext>()));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateExpenseCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(CreateExpenseCommand).Assembly);
builder.Services.AddFluentValidationAutoValidation();

if (!isTesting)
{
    builder.Services.AddRabbitMqMessaging(rabbitMqOptions);

    var eventsExchange = EventBusConstants.GetEventsExchangeName();
    builder.Services.AddRabbitMqOutboxRelayTransport(eventsExchange);
    builder.Services.AddRabbitMqEventConsumer<DataIngestedEvent>(EventBusConstants.QueueDataIngested, eventsExchange);
    builder.Services.AddRabbitMqEventConsumer<PriceAnalyzedEvent>(EventBusConstants.QueuePriceAnalyzed, eventsExchange);

    builder.Services.AddHostedService<DataIngestedConsumer>();
    builder.Services.AddHostedService<PriceAnalyzedConsumer>();
    builder.Services.AddHostedService<OutboxRelayWorker>();
    builder.Services.AddHostedService<DashboardRefreshWorker>();
}


builder.Services.AddProcessingProblemDetails();

var app = builder.Build();

app.ValidateSimcagGatewayTrustAtStartup();

app.UseSimcagExceptionHandler();
app.UseSimcagHttpCorrelationActivityTags();

if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ProcessingDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            app.Logger.LogInformation(
                "Aplicando {Count} migration(s) pendente(s) no ProcessingDbContext.",
                pending.Count());
            await db.Database.MigrateAsync();
        }
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

if (!isTesting)
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapSimcagHealthChecks();

app.UseSimcagTelemetryEndpoints();

app.Run();

public partial class Program
{
}
