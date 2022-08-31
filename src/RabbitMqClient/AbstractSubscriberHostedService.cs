using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMqClient.Internal;

namespace RabbitMqClient;

public abstract class AbstractSubscriberHostedService<TMessageHandler, TMessage, THandledException> : MessageQueueSubscriber<TMessageHandler, TMessage, THandledException>, IHostedService 
    where TMessageHandler : IMessageHandler<TMessage> where THandledException : Exception
{
    private readonly SubscriptionInfo _subscriptionInfo;

    protected AbstractSubscriberHostedService(IConnectionFactory connectionFactory, IConsumerFactory consumerFactory, ILogger logger, string clientName, SubscriptionInfo subscriptionInfo)
        : base(connectionFactory, consumerFactory, logger, clientName)
    {
        _subscriptionInfo = subscriptionInfo;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.Run(() => Start(_subscriptionInfo, cancellationToken));

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.Run(Stop, cancellationToken);
}