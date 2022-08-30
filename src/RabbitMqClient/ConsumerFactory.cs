using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqClient.Internal;

namespace RabbitMqClient;



public interface IConsumerFactory
{
    IRetryBasicConsumer<THandledException> CreateRetryOnExceptionConsumer<THandledException>(IModel channel,
                                                                                             SubscriptionInfo subscriptionInfo,
                                                                                             Func<IServiceProvider, BasicDeliverEventArgs, CancellationToken, Task<IProcessingOutcome>> messageHandler,
                                                                                             Action<BasicDeliverEventArgs, Exception, int>? retryHandler,
                                                                                             CancellationToken cancellationToken) where THandledException : Exception;
}

internal class ConsumerFactory : IConsumerFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILoggerFactory _loggerFactory;

    public ConsumerFactory(IServiceScopeFactory serviceScopeFactory, ILoggerFactory loggerFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _loggerFactory = loggerFactory;
    }

    public IRetryBasicConsumer<THandledException> CreateRetryOnExceptionConsumer<THandledException>(IModel channel,
                                                                                                    SubscriptionInfo subscriptionInfo,
                                                                                                    Func<IServiceProvider, BasicDeliverEventArgs, CancellationToken, Task<IProcessingOutcome>> messageHandler,
                                                                                                    Action<BasicDeliverEventArgs, Exception, int>? retryHandler,
                                                                                                    CancellationToken cancellationToken) where THandledException : Exception
        => new RetryAsyncConsumer<THandledException>(channel, 
                                                     _serviceScopeFactory, 
                                                     subscriptionInfo, 
                                                     messageHandler,
                                                     retryHandler,
                                                     _loggerFactory.CreateLogger<RetryAsyncConsumer<THandledException>>(), 
                                                     cancellationToken);
}