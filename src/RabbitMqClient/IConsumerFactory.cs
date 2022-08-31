using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace RabbitMqClient
{
    public interface IConsumerFactory
    {
        IRetryBasicConsumer<THandledException> CreateRetryOnExceptionConsumer<THandledException>(IModel channel,
                                                                                                 SubscriptionInfo subscriptionInfo,
                                                                                                 Func<IServiceProvider, BasicDeliverEventArgs, CancellationToken, Task<IProcessingOutcome>> messageHandler,
                                                                                                 Action<BasicDeliverEventArgs, Exception, int>? retryHandler,
                                                                                                 CancellationToken cancellationToken) where THandledException : Exception;
    }
}
