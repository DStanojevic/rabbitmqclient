using System.Collections.Concurrent;
using RabbitMqClient;
using SubscriberTestApp.Internal.Repositories;
using TestAppCommon;

namespace SubscriberTestApp.Internal.Subscribers;

public interface IRetryingMessageHandler : IMessageHandler<MqGenericMessage<RetryInfo>>
{
}

public class RetryingMessageHandler : IRetryingMessageHandler
{
    private readonly IMessagesRepository _messagesRepository;
    private readonly ILogger<RetryingMessageHandler> _logger;

    public RetryingMessageHandler(IMessagesRepository messagesRepository, ILogger<RetryingMessageHandler> logger)
    {
        _messagesRepository = messagesRepository;
        _logger = logger;
    }

    private static readonly ConcurrentDictionary<string, int> _messageRetries = new();

    public async Task<IProcessingOutcome> HandleMessage(IMessageQueueSubscriber subscriber, 
                                                        IDictionary<string, object> headers,
                                                        MqGenericMessage<RetryInfo> genericMessage, 
                                                        CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(
                   $"Processing genericMessage with id {genericMessage.Attributes.MessageId}. Topic: {subscriber.Subscription.Topic.TopicName}, Subscription: {subscriber.Subscription.SubscriptionName}."))
        {

            var attempt = _messageRetries.GetOrAdd(genericMessage.Attributes.MessageId, 0);
            _messageRetries[genericMessage.Attributes.MessageId] = ++attempt;

            if (genericMessage.Payload.RetryCount >= attempt)
                throw new RetryException(attempt);

            if (genericMessage.Payload.AlwaysThrow)
                throw new InvalidOperationException("This message shall not pass.");

            genericMessage.Payload = new RetryInfo
            {
                RetryCount = genericMessage.Payload.RetryCount,
                AttemptedCount = attempt
            };

            await _messagesRepository.AddMessage(genericMessage);

            return new AcknowledgedOutcome();
        }
    }
}