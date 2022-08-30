﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqClient;

public interface IMessageQueueSubscriber
{
    void Start(SubscriptionInfo subscriptionInfo);
    void Stop();
    string ClientName { get; set; }
    SubscriptionInfo Subscription { get; }
}

public class MessageQueueSubscriber<TMessageHandler, TMessage, THandledException> : IMessageQueueSubscriber, IDisposable 
    where TMessageHandler : IMessageHandler<TMessage> where THandledException : Exception
{
    private SubscriptionInfo _subscription;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IConsumerFactory _consumerFactory;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;

    public MessageQueueSubscriber(IConnectionFactory connectionFactory, IConsumerFactory consumerFactory, ILogger logger, string clientName)
    {
        _connectionFactory = connectionFactory;
        _consumerFactory = consumerFactory;
        ClientName = clientName;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public SubscriptionInfo Subscription => _subscription;

    private TMessage DeserializeMessageBody(ReadOnlySpan<byte> messageBytes)
    {
        var message = Encoding.UTF8.GetString(messageBytes);
        var objectResult = JsonSerializer.Deserialize<TMessage>(message);
        if (objectResult == null)
            throw new InvalidOperationException("Message content cannot be null");

        return objectResult;
    }

    public void Start(SubscriptionInfo subscriptionInfo)
    {
        using (_logger.BeginScope(
                   $"RabbitMQ subscriber client starting listening.Exchange: {_subscription.Topic.TopicName}, queue: {_subscription.SubscriptionName}."))
        {
            if (_channel != null && _channel.IsOpen)
                throw new InvalidOperationException("Subscription channelWrapper is already opened.");
            _subscription = subscriptionInfo;
            if (_connection == null || !_connection.IsOpen)
                _connection = _connectionFactory.CreateConnection();

            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(subscriptionInfo.Topic.TopicName, Globals.ExchangeType, subscriptionInfo.Topic.Durable, subscriptionInfo.Topic.AutoDelete);

            var additionalQueueArguments = new Dictionary<string, object>();

            if (!_subscription.Temporary)
                additionalQueueArguments.Add("x-queue-type", "quorum");

            if (_subscription.DeadLetterTopic != null)
            {
                RegisterDeadLetterExchange(_channel, _subscription.DeadLetterTopic.Name);
                additionalQueueArguments.Add("x-dead-letter-exchange", _subscription.DeadLetterTopic.Name);
            }

            _ = _channel.QueueDeclare(_subscription.SubscriptionName,
                                      durable: !_subscription.Temporary,
                                      exclusive: false,
                                      autoDelete: _subscription.Temporary,
                                      additionalQueueArguments);

            _channel.BasicQos(0, _subscription.MaxMessagesInParallel, false);
            //Bind queue with general routing key
            _channel.QueueBind(_subscription.SubscriptionName, _subscription.Topic.TopicName, Globals.GeneralRoutingKey, null);
            //Bind queue with queue name routing key. This binding is used when want to return message from DLE.
            _channel.QueueBind(_subscription.SubscriptionName, _subscription.Topic.TopicName, _subscription.SubscriptionName, null);

            var consumer = _consumerFactory.CreateRetryOnExceptionConsumer<THandledException>(_channel, Subscription, OnMessageReceived, OnRetry, _cancellationTokenSource.Token);

            _ = _channel.BasicConsume(_subscription.SubscriptionName, _subscription.AutoAck, consumer);

            _logger.LogInformation(
                $"RabbitMQ subscriber started listening. Exchange: {_subscription.Topic.TopicName}, queue: {_subscription.SubscriptionName}.");
        }
    }

    public void Stop()
    {
        using (_logger.BeginScope(
                   $"Stopping RabbitMQ subscriber client. Exchange: {_subscription.Topic.TopicName}, queue: {_subscription.SubscriptionName}."))
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _channel?.Close();
            _connection?.Close();
            _logger.LogInformation(
                $"Stopped RabbitMQ subscriber client. Exchange: {_subscription.Topic.TopicName}, queue: {_subscription.SubscriptionName}.");
        }
    }

    public string ClientName
    {
        get => _connectionFactory.ClientProvidedName;
        set => _connectionFactory.ClientProvidedName = value;
    }

    private Task<IProcessingOutcome> OnMessageReceived(IServiceProvider serviceProvider, BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(
                   $"Handle message. Topic: {eventArgs.Exchange}. Subscription {Subscription.SubscriptionName}. MessageId tag {eventArgs.BasicProperties.MessageId}. Routing key: {eventArgs.RoutingKey}"))
        {
            var messageBody = DeserializeMessageBody(eventArgs.Body.Span);
            var messageHandler = serviceProvider.GetService<TMessageHandler>() ??
                                 throw new NotSupportedException($"Message handler of type '{typeof(TMessageHandler)}' not supported.");
            return messageHandler.HandleMessage(this, eventArgs.BasicProperties.Headers, messageBody, cancellationToken);
        }
    }

    public virtual void OnRetry(BasicDeliverEventArgs deliverEventArgs, Exception ex, int attempt)
    {
        //no default implementation
    }

    private static void RegisterDeadLetterExchange(IModel channel, string exchangeName)
    {
        var queueTypeArguments = new Dictionary<string, object> {{"x-queue-type", "quorum"}};
        var queueName = $"{exchangeName}-DefaultQueue";
        channel.ExchangeDeclare(exchangeName, Globals.DeadLetterExchangeType, durable: true, autoDelete: false);
        _ = channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, queueTypeArguments);
        channel.QueueBind(queueName, exchangeName, string.Empty, null);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
            _channel.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}