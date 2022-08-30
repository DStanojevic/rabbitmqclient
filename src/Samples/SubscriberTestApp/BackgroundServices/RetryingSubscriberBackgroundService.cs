using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class RetryingSubscriberBackgroundService : AbstractSubscriberHostedService<IRetryingMessageHandler, MqGenericMessage<RetryInfo>, RetryException>
{
    public RetryingSubscriberBackgroundService(IConnectionFactory connectionFactory,
                                               IConsumerFactory consumerFactory,
                                               ILogger<RetryingSubscriberBackgroundService> logger,
                                               RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, SubscriberName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string SubscriberName = "app:SubscriberTestApp component:RetryingSubscriberBackgroundService";

    public const string SubscriptionName = "TestRetryTopic-Subscription1";
}