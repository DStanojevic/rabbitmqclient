using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class TestTopicSubscriberBackgroundService : AbstractSubscriberHostedService<ISimpleMessageHandler, MqGenericMessage<string>, Exception>
{
    public TestTopicSubscriberBackgroundService(IConnectionFactory connectionFactory, 
                                                IConsumerFactory consumerFactory, 
                                                ILogger<TestTopicSubscriberBackgroundService> logger, 
                                                RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, SubscriberName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string SubscriberName = "TestTopicSubscriber";

    public const string SubscriptionName = "TestTopic-Subscription1";
}