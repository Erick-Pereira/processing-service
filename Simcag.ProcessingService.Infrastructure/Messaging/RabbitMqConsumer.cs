using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Simcag.ProcessingService.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

public class RabbitMqConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RabbitMqOptions _options;

    public RabbitMqConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConsumer> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                UserName = _options.UserName,
                Password = _options.Password,
                Port = _options.Port
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: "price-events", durable: true, exclusive: false, autoDelete: false, arguments: null);

            _logger.LogInformation("RabbitMQ Consumer connection established.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ Consumer connection.");
            throw;
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                _logger.LogInformation("Received message from RabbitMQ: {Message}", message);

                using var scope = _serviceScopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IProcessingService>();

                var priceEvent = JsonSerializer.Deserialize<shared.Events.PriceCollectedEvent>(message);
                if (priceEvent != null)
                {
                    await processingService.ProcessPriceEventAsync(priceEvent);
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("Message processed successfully for ProductId: {ProductId}", priceEvent.ProductId);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize message: {Message}", message);
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing RabbitMQ message.");
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: "price-events", autoAck: false, consumer: consumer);

        _logger.LogInformation("RabbitMQ Consumer started listening on queue: price-events");

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}