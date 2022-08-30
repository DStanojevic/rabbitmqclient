using System;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace RabbitMqClient.Internal;

internal interface IRabbitMqChannelWrapper
{
    void SendMessage(TopicInfo topicInfo, ReadOnlyMemory<byte> messageBody, IBasicProperties? basicProperties = null);
}

internal sealed class RabbitMqChannelWrapper : IRabbitMqChannelWrapper, IDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private IConnection _connection;
    private IModel _channel;

    private readonly HashSet<TopicInfo> _topics = new();

    public RabbitMqChannelWrapper(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private void EnsureConnection()
    {
        lock (_connectionFactory)
        {
            if (_connection == null || !_connection.IsOpen || _channel == null || _channel.IsClosed)
            {
                _connection = _connectionFactory.CreateConnection();
                _channel = _connection.CreateModel();
            }
        }
    }

    public void SendMessage(TopicInfo topicInfo, ReadOnlyMemory<byte> messageBody,
        IBasicProperties? basicProperties = null)
    {
        EnsureConnection();
        //Due that _channelWrapper (IModel) instance is not intended to be used by more than one thread simultaneously, need to introduce thread synchronization.
        lock (_channel)
        {
            if (!_topics.TryGetValue(topicInfo, out var currentTopicInfo))
            {
                currentTopicInfo = topicInfo;
                _channel.ExchangeDeclare(currentTopicInfo.TopicName, Globals.ExchangeType, currentTopicInfo.Durable,
                    currentTopicInfo.AutoDelete);
                _topics.Add(currentTopicInfo);
            }

            _channel.BasicPublish(currentTopicInfo.TopicName,
                routingKey: Globals.GeneralRoutingKey,
                basicProperties: basicProperties,
                body: messageBody);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _channel?.Dispose();
        GC.SuppressFinalize(this);
    }

    ~RabbitMqChannelWrapper()
    {
        Dispose();
    }
}