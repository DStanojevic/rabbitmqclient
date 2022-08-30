using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class SimplePubSubSubscriberBackgroundService2 : AbstractSubscriberHostedService<IMessageIdPrependMessageHandler, MqGenericMessage<string>, Exception>
{
    public SimplePubSubSubscriberBackgroundService2(IConnectionFactory connectionFactory,
                                                    IConsumerFactory consumerFactory,
                                                    ILogger<SimplePubSubSubscriberBackgroundService2> logger,
                                                    RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, SubscriberName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }


    public const string SubscriberName = "subscriber2";
    public const string SubscriptionName = "PubSubTopic1-Subscription2";
}