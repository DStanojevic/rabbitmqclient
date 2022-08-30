using RabbitMqClient;
using SubscriberTestApp.Internal.Repositories;
using TestAppCommon;

namespace SubscriberTestApp.Internal.Subscribers;

public interface ISimpleMessageHandler : IMessageHandler<MqGenericMessage<string>>
{

}

public class SimpleMessageHandler :  ISimpleMessageHandler
{
    private readonly IMessagesRepository _messagesRepository;
    private readonly ILogger<SimpleMessageHandler> _logger;
    public SimpleMessageHandler(IMessagesRepository messagesRepository, ILogger<SimpleMessageHandler> logger)
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
            await _messagesRepository.AddMessage(message);
            _logger.LogInformation($"Added message with id {message.Attributes.MessageId}");
            return new AcknowledgedOutcome();
        }
    }
}