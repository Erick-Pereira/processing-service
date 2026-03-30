using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Simcag.ProcessingService.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace Simcag.ProcessingService.Infrastructure.Messaging;

public class MessagePublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<MessagePublisher> _logger;

    public MessagePublisher(IOptions<RabbitMqOptions> options, ILogger<MessagePublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = options.Value.Host,
                UserName = options.Value.UserName,
                Password = options.Value.Password,
                Port = options.Value.Port
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: "data.processed", durable: true, exclusive: false, autoDelete: false, arguments: null);

            _logger.LogInformation("RabbitMQ Publisher connection established.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ Publisher connection.");
            throw;
        }
    }

    public async Task PublishAsync<T>(string queueName, T message)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            _channel.BasicPublish(exchange: "",
                                 routingKey: queueName,
                                 basicProperties: null,
                                 body: body);

            _logger.LogInformation("Message published to RabbitMQ queue: {QueueName}", queueName);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ.");
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}