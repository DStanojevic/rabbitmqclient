using RabbitMqClient;
using SubscriberTestApp.Internal.Repositories;
using TestAppCommon;

namespace SubscriberTestApp.Internal.Subscribers;


public interface IMessageIdPrependMessageHandler : IMessageHandler<MqGenericMessage<string>>
{
}

public class MessageIdPrependMessageHandler : IMessageIdPrependMessageHandler
{
    private readonly IMessagesRepository _messagesRepository;
    private readonly ILogger<MessageIdPrependMessageHandler> _logger;
    public MessageIdPrependMessageHandler(IMessagesRepository messagesRepository, ILogger<MessageIdPrependMessageHandler> logger)
    {
        _messagesRepository = messagesRepository;
        _logger = logger;
    }
    
    public async Task<IProcessingOutcome> HandleMessage(IMessageQueueSubscriber subscriber, 
                                                        IDictionary<string, object> headers,
                                                        MqGenericMessage<string> message,
                                                        CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(
                   $"Processing message with id {message.Attributes.MessageId}. Topic: {subscriber.Subscription.Topic.TopicName}, Subscription: {subscriber.Subscription.SubscriptionName}."))
        {
            message.Attributes.MessageId = $"{subscriber.ClientName}.{message.Attributes.MessageId}";

            await _messagesRepository.AddMessage(message);
            _logger.LogInformation($"Added message with id {message.Attributes.MessageId}");
            return new AcknowledgedOutcome();
        }
    }

}