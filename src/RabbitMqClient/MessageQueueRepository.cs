using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMqClient.Configuration;

namespace RabbitMqClient;

public interface IMessageQueueRepository
{
    Task<IEnumerable<RabbitMqMessageData>> GetMessagesAsync(string queueName);
}

internal class MessageQueueRepository : IMessageQueueRepository, IDisposable
{
    private IConnection _connection;
    private readonly IModel _channel;

    public MessageQueueRepository(RabbitMqConfiguration configuration)
    {
        var factory = new ConnectionFactory()
        {
            HostName = configuration.HostName,
            UserName = configuration.UserName,
            Password = configuration.Password,
        };
        if (!string.IsNullOrEmpty(configuration.ClientName))
            factory.ClientProvidedName = configuration.ClientName;

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    private IEnumerable<RabbitMqMessageData> GetMessages(string queueName)
    {
        var messages = new List<RabbitMqMessageData>();
        BasicGetResult? result;
        while ((result = _channel.BasicGet(queueName, false)) != null)
        {
            var message = new RabbitMqMessageData(result.DeliveryTag,
                                                  result.Exchange,
                                                  result.RoutingKey,
                                                  Encoding.UTF8.GetString(result.Body.Span),
                                                  result.BasicProperties.Headers,
                                                  result.BasicProperties.AppId,
                                                  result.BasicProperties.MessageId,
                                                  result.BasicProperties.ContentEncoding,
                                                  result.BasicProperties.ContentType,
                                                  result.BasicProperties.Type,
                                                  result.BasicProperties.Expiration);
            messages.Add(message);
        }

        return messages;
    }

    public Task<IEnumerable<RabbitMqMessageData>> GetMessagesAsync(string queueName)
        => Task.Run(() => GetMessages(queueName));

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _channel.Dispose();
            _connection.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}