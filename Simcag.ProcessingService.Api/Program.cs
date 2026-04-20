using Microsoft.EntityFrameworkCore;
using Simcag.IngestionService.Domain.Events;
using Simcag.ProcessingService.Api.Workers;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.Services;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Persistence.Repositories;
using Simcag.ProcessingService.Infrastructure.Services;
using Simcag.Shared.Messaging.Configuration;
using Simcag.Shared.Messaging.Extensions;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL")
    .AddRabbitMQ(rabbitMqOptions.ToConnectionString(), name: "RabbitMQ");

builder.Services.AddDbContext<ProcessingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProcessingService, ProcessingService>();
builder.Services.AddScoped<IIdempotencyChecker, IdempotencyChecker>();

builder.Services.AddRabbitMqMessaging(rabbitMqOptions, "simcag-events");
builder.Services.AddRabbitMqEventConsumer<PriceCollectedEvent>("price-events");

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