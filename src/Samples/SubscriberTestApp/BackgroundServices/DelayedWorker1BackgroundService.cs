using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class DelayedWorker1BackgroundService : AbstractSubscriberHostedService<IDelayedWorkMessageHandler, MqGenericMessage<DelayInfo>, RetryException>
{
    public DelayedWorker1BackgroundService(IConnectionFactory connectionFactory,
                                           IConsumerFactory consumerFactory,
                                           ILogger<DelayedWorker1BackgroundService> logger,
                                           RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, WorkerName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string WorkerName = "WORKER1";
    public const string SubscriptionName = "ThrottlingWorkerTestTopic1-Subscription1";
}