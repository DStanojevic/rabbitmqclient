using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class DelayedWorker2BackgroundService : AbstractSubscriberHostedService<IDelayedWorkMessageHandler, MqGenericMessage<DelayInfo>, RetryException>
{
    public DelayedWorker2BackgroundService(IConnectionFactory connectionFactory,
                                           IConsumerFactory consumerFactory,
                                           ILogger<DelayedWorker2BackgroundService> logger,
                                           RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, WorkerName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string WorkerName = "WORKER2";

    public const string SubscriptionName = "ThrottlingWorkerTestTopic1-Subscription1";
}