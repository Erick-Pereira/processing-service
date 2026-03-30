using Microsoft.EntityFrameworkCore;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Application.Services;
using Simcag.ProcessingService.Infrastructure.Messaging;
using Simcag.ProcessingService.Infrastructure.Persistence;
using Simcag.ProcessingService.Infrastructure.Repositories;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Bind para Docker (ESSENCIAL)
builder.WebHost.UseUrls("http://localhost:8081");

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core with PostgreSQL
builder.Services.AddDbContext<ProcessingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// RabbitMQ Options
builder.Services.Configure<RabbitMqOptions>(options =>
{
    options.Host = builder.Configuration["RabbitMQ:Host"];
    options.UserName = builder.Configuration["RabbitMQ:UserName"];
    options.Password = builder.Configuration["RabbitMQ:Password"];
    options.Port = int.Parse(builder.Configuration["RabbitMQ:Port"]);
});

// Repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Message Publisher
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Processing Service
builder.Services.AddScoped<IProcessingService, ProcessingServiceImpl>();

// RabbitMQ Consumer (Background Service)
builder.Services.AddHostedService<RabbitMqConsumer>();

// Logging
builder.Services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProcessingDbContext>();
    dbContext.Database.EnsureCreated();
}

// Swagger sempre ativo (pra debug)
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();
