using RabbitMQ.Client;
using RabbitMqClient;
using RabbitMqClient.Configuration;
using SubscriberTestApp.Internal.Subscribers;
using TestAppCommon;

namespace SubscriberTestApp.BackgroundServices;

public class Worker1BackgroundService : AbstractSubscriberHostedService<IMessageIdPrependMessageHandler, MqGenericMessage<string>, Exception>
{
    public Worker1BackgroundService(IConnectionFactory connectionFactory,
                                    IConsumerFactory consumerFactory,
                                    ILogger<Worker1BackgroundService> logger, 
                                    RabbitMqSubscriptionsConfiguration configuration)   
        : base(connectionFactory, consumerFactory, logger, WorkerName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string WorkerName = "worker1";

    public const string SubscriptionName = "WorkerTestTopic1-Subscription1";
}