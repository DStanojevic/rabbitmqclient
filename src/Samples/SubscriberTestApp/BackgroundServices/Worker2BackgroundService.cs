using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;


namespace SubscriberTestApp.BackgroundServices;

public class Worker2BackgroundService : AbstractSubscriberHostedService<IMessageIdPrependMessageHandler, MqGenericMessage<string>, Exception>
{
    public Worker2BackgroundService(IConnectionFactory connectionFactory,
                                    IConsumerFactory consumerFactory,
                                    ILogger<Worker2BackgroundService> logger,
                                    RabbitMqSubscriptionsConfiguration configuration)
        : base(connectionFactory, consumerFactory, logger, WorkerName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string WorkerName = "worker2";

    public const string SubscriptionName = "WorkerTestTopic1-Subscription1";
}