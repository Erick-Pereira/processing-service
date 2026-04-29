using Microsoft.EntityFrameworkCore;
using Simcag.Shared.Events;
using Simcag.ProcessingService.Api.Workers;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.Services;
using Simcag.ProcessingService.Application.UseCases;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Persistence.Repositories;
using Simcag.ProcessingService.Infrastructure.Services;
using Simcag.Shared.Messaging;
using Simcag.Shared.Messaging.Configuration;
using Simcag.Shared.Messaging.Extensions;
using PriceDataProcessedEvent = Simcag.ProcessingService.Domain.Events.PriceDataProcessedEvent;

DotNetEnv.Env.Load();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "SIMC-AG Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name        = "Authorization",
        In          = Microsoft.OpenApi.ParameterLocation.Header,
        Type        = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        Description = "Cole apenas o JWT (sem 'Bearer ')."
    });
    c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// ✅ RabbitMQ via ENV
var rabbitMqOptions = new RabbitMqOptions
{
    Host = Environment.GetEnvironmentVariable("RABBITMQ__HOST") ?? throw new InvalidOperationException("RABBITMQ__HOST not set"),
    Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ__PORT") ?? "5672"),
    UserName = Environment.GetEnvironmentVariable("RABBITMQ__USERNAME") ?? throw new InvalidOperationException("RABBITMQ__USERNAME not set"),
    Password = Environment.GetEnvironmentVariable("RABBITMQ__PASSWORD") ?? throw new InvalidOperationException("RABBITMQ__PASSWORD not set"),
    VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ__VIRTUALHOST") ?? "/"
};

// ✅ DB via ENV
var connectionString = $"Host={Environment.GetEnvironmentVariable("DB__HOST")};" +
                      $"Port={Environment.GetEnvironmentVariable("DB__PORT")};" +
                      $"Database={Environment.GetEnvironmentVariable("DB__NAME")};" +
                      $"Username={Environment.GetEnvironmentVariable("DB__USER")};" +
                      $"Password={Environment.GetEnvironmentVariable("DB__PASSWORD")}";

// Health Checks: AspNetCore.HealthChecks.Rabbitmq 8.x exige um URI AMQP padrão.
var rabbitMqHealthUri = $"amqp://{Uri.EscapeDataString(rabbitMqOptions.UserName)}:{Uri.EscapeDataString(rabbitMqOptions.Password)}@{rabbitMqOptions.Host}:{rabbitMqOptions.Port}{(rabbitMqOptions.VirtualHost == "/" ? "/" : "/" + rabbitMqOptions.VirtualHost.TrimStart('/'))}";

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL")
    .AddRabbitMQ(rabbitMqHealthUri, name: "RabbitMQ");

builder.Services.AddDbContext<ProcessingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IProcessingService, ProcessingService>();
builder.Services.AddScoped<IIdempotencyChecker, IdempotencyChecker>();
builder.Services.AddScoped<ProcessPriceCollectedEventUseCase>();
// Adapta o publisher canônico (Simcag.Shared.Messaging.IEventPublisher) para o IEventPublisher local da Application.
builder.Services.AddScoped<Simcag.ProcessingService.Application.Interfaces.IEventPublisher, Simcag.ProcessingService.Application.Adapters.SharedEventPublisherAdapter>();

builder.Services.AddRabbitMqMessaging(rabbitMqOptions);

var eventsExchange = EventBusConstants.GetEventsExchangeName();
// Nome da fila mantido por compatibilidade com topologia já criada (o 1º parâmetro é queueName, não exchange).
builder.Services.AddRabbitMqEventConsumer<PriceCollectedEvent>("simcag-events", eventsExchange);
builder.Services.AddRabbitMqEventPublisher<PriceDataProcessedEvent>(eventsExchange);

// ✅ Background Service Worker
builder.Services.AddHostedService<PriceProcessingBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();