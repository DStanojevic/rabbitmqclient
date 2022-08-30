using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMqClient.Internal;

namespace RabbitMqClient;

public interface IMessageQueuePublisher
{
    Task SendMessageAsync<TMessage>(TopicInfo topicInfo, TMessage messageBody);
    Task SendMessagesAsync<TMessage>(TopicInfo topicInfo, IEnumerable<TMessage> messages);
}

internal class MessageQueuePublisher : IMessageQueuePublisher
{
    private readonly IRabbitMqChannelWrapper _channelWrapper;
    private readonly ILogger<MessageQueuePublisher> _logger;

    public MessageQueuePublisher(IRabbitMqChannelWrapper channelWrapper, ILogger<MessageQueuePublisher> logger)
    {
        _channelWrapper = channelWrapper;
        _logger = logger;
    }

    private byte[] SerializeMessage<TMessage>(TMessage messageBody)
        => JsonSerializer.SerializeToUtf8Bytes(messageBody);

    public Task SendMessageAsync<TMessage>(TopicInfo topicInfo, TMessage messageBody)
        => Task.Run(() =>
        {
            _channelWrapper.SendMessage(topicInfo, SerializeMessage(messageBody));
            _logger.LogInformation($"Messages successfully published to topic: {topicInfo.TopicName}.");
        });

    /// <summary>
    /// Sending messages asynchronously by "fire and forget" principle.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="topicInfo"></param>
    /// <param name="messages"></param>
    public Task SendMessagesAsync<TMessage>(TopicInfo topicInfo, IEnumerable<TMessage> messages)
    {
        return Task.Run(() =>
        {
            var messagesCount = 0;
            try
            {
                foreach (var message in messages)
                {
                    _channelWrapper.SendMessage(topicInfo, SerializeMessage(message));
                    messagesCount++;
                }

                _logger.LogInformation(
                    $"{messagesCount} messages successfully published to topic: {topicInfo.TopicName}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed publishing messages to topic {topicInfo.TopicName}.");
                throw;
            }
        });
    }
}