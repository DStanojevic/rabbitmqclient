using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class SimplePubSubSubscriberBackgroundService1 : AbstractSubscriberHostedService<IMessageIdPrependMessageHandler, MqGenericMessage<string>, Exception>
{
    public SimplePubSubSubscriberBackgroundService1(IConnectionFactory connectionFactory,
                                                    IConsumerFactory consumerFactory,
                                                    ILogger<SimplePubSubSubscriberBackgroundService1> logger,
                                                    RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, SubscriberName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string SubscriberName = "subscriber1";

    public const string SubscriptionName = "PubSubTopic1-Subscription1";
}