using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMqClient.Internal;

internal sealed class RetryAsyncConsumer<THandledException> : AsyncDefaultBasicConsumer, IRetryBasicConsumer<THandledException>
    where THandledException : Exception
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly Func<IServiceProvider, BasicDeliverEventArgs, CancellationToken, Task<IProcessingOutcome>>
        _messageHandler;

    private readonly Action<BasicDeliverEventArgs, Exception, int>? _retryExternalHandler;
    private readonly SubscriptionInfo _subscriptionInfo;
    private readonly ILogger<IRetryBasicConsumer<THandledException>> _logger;
    private readonly CancellationToken _cancellationToken;

    public RetryAsyncConsumer(IModel model,
        IServiceScopeFactory serviceScopeFactory,
        SubscriptionInfo subscriptionInfo,
        Func<IServiceProvider, BasicDeliverEventArgs, CancellationToken, Task<IProcessingOutcome>> messageHandlerFunc,
        Action<BasicDeliverEventArgs, Exception, int>? retryExternalHandler,
        ILogger<IRetryBasicConsumer<THandledException>> logger,
        CancellationToken cancellationToken)
        : base(model)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _subscriptionInfo = subscriptionInfo;
        _messageHandler = messageHandlerFunc ?? throw new ArgumentNullException(nameof(messageHandlerFunc));
        _retryExternalHandler = retryExternalHandler;
        _logger = logger;
        _cancellationToken = cancellationToken;
    }

    public override Task HandleBasicDeliver(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        IBasicProperties properties,
        ReadOnlyMemory<byte> body)
    {
        return HandleDeliverAsync(new BasicDeliverEventArgs(consumerTag,
            deliveryTag,
            redelivered,
            exchange,
            routingKey,
            properties,
            body));
    }

    private async Task HandleDeliverAsync(BasicDeliverEventArgs deliverEventArgs)
    {
        var retryPolicy = BuildRetryPolicy(deliverEventArgs);
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            PolicyResult<IProcessingOutcome> executionResult;
            var policyContext = new Context();
            try
            {
                executionResult =
                    await HandleDeliver(scope.ServiceProvider, deliverEventArgs, policyContext, retryPolicy);
            }
            catch (Exception ex)
            {
                executionResult = PolicyResult<IProcessingOutcome>.Failure(ex, ExceptionType.Unhandled, policyContext);
                _logger.LogError(
                    $"Failed to handle RabbitMQ message from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, messageId: {deliverEventArgs.BasicProperties.MessageId}, routing key: {deliverEventArgs.RoutingKey}, exception: {ex}. Outcome type: {executionResult.Outcome}");
            }

            var outcome = executionResult.Outcome == OutcomeType.Failure
                ? new FailureProcessingOutcome(executionResult.FinalException)
                : executionResult.Result;
            try
            {
                HandleProcessingOutcome(deliverEventArgs, outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Failed to handle RabbitMQ message processing outcome from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, messageId: {deliverEventArgs.BasicProperties.MessageId}, routing key: {deliverEventArgs.RoutingKey}, exception: {ex}. Outcome type: {outcome.GetType().Name}");
                HandleRejectedOutcome(deliverEventArgs, new RejectedOutcome($"Unhandled exception occured: {ex}"));
                throw;
            }
        }
    }

    private Task<PolicyResult<IProcessingOutcome>> HandleDeliver(IServiceProvider serviceProvider,
        BasicDeliverEventArgs deliverEventArgs,
        Context context,
        AsyncPolicy retryPolicy)
        => retryPolicy.ExecuteAndCaptureAsync((_, ct) => _messageHandler(serviceProvider, deliverEventArgs, ct),
            context, _cancellationToken);

    private AsyncPolicy BuildRetryPolicy(BasicDeliverEventArgs deliverEventArgs)
    {
        if (_subscriptionInfo.RetryCount > 0)
            return Policy.Handle<THandledException>()
                .RetryAsync((int) _subscriptionInfo.RetryCount,
                    (ex, attempt, context) => OnRetry(deliverEventArgs, ex, attempt, context));

        return Policy.NoOpAsync();
    }

    private void OnRetry(BasicDeliverEventArgs deliverEventArgs, Exception ex, int attempt, Context context)
    {
        _logger.LogInformation(
            $"Trying to handle message from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, attempt: {attempt}, messageId: {deliverEventArgs.BasicProperties?.MessageId}, routing key: {deliverEventArgs.RoutingKey}, redelivered:  {deliverEventArgs.Redelivered}");
        if (_retryExternalHandler != null)
        {
            try
            {
                _retryExternalHandler(deliverEventArgs, ex, attempt);
            }
            catch (Exception retryHandlerEx)
            {
                _logger.LogError("Error occurred during external retry handling. Exception will be not thrown.",
                    retryHandlerEx);
            }
        }
    }

    private void HandleProcessingOutcome(BasicDeliverEventArgs deliverEventArgs, IProcessingOutcome processingOutcome)
    {
        switch (processingOutcome)
        {
            case FailureProcessingOutcome failureProcessingOutcome:
                HandleFailure(deliverEventArgs, failureProcessingOutcome);
                break;
            case NegativelyAcknowledgedOutcome negativellyAcknowledgedOutcome:
                HandleNegativeAcknowledgment(deliverEventArgs, negativellyAcknowledgedOutcome);
                break;
            case RejectedOutcome rejectedAknowledged:
                HandleRejectedOutcome(deliverEventArgs, rejectedAknowledged);
                break;
            case AcknowledgedOutcome aknowledgetOutcome:
                HandleAcknowledgment(deliverEventArgs, aknowledgetOutcome);
                break;
            default:
                throw new NotSupportedException($"Not supported outcome type: {processingOutcome.GetType().FullName}");
        }
    }

    private void HandleFailure(BasicDeliverEventArgs deliverEventArgs, FailureProcessingOutcome processingOutcome)
    {
        _logger.LogWarning(
            $"Failure in processing message from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, messageId: {deliverEventArgs.BasicProperties.MessageId}, routing key: {deliverEventArgs.RoutingKey}, exception: {processingOutcome.Error}");
        lock (Model)
        {
            var requeue = ShouldRequeue(deliverEventArgs);
            Model.BasicNack(deliverEventArgs.DeliveryTag, false, requeue);
        }
    }

    private void HandleNegativeAcknowledgment(BasicDeliverEventArgs deliverEventArgs,
        NegativelyAcknowledgedOutcome processingOutcome)
    {
        _logger.LogInformation(
            $"Negatively acknowledged message from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, messageId: {deliverEventArgs.BasicProperties.MessageId}, routing key: {deliverEventArgs.RoutingKey}, reason: {processingOutcome.Reason}");
        lock (Model)
        {
            var requeue = ShouldRequeue(deliverEventArgs);
            Model.BasicNack(deliverEventArgs.DeliveryTag, false, requeue);
        }
    }

    private void HandleRejectedOutcome(BasicDeliverEventArgs deliverEventArgs, RejectedOutcome processingOutcome)
    {
        _logger.LogInformation(
            $"Rejected message from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, messageId: {deliverEventArgs.BasicProperties.MessageId}, routing key: {deliverEventArgs.RoutingKey}, reason: {processingOutcome.Reason}");
        lock (Model)
        {
            var requeue = ShouldRequeue(deliverEventArgs);
            Model.BasicReject(deliverEventArgs.DeliveryTag, requeue);
        }
    }

    private void HandleAcknowledgment(BasicDeliverEventArgs deliverEventArgs, AcknowledgedOutcome processingOutcome)
    {
        if (!_subscriptionInfo.AutoAck)
        {
            lock (Model)
            {
                Model.BasicAck(deliverEventArgs.DeliveryTag, false);
            }
        }
    }

    private bool ShouldRequeue(BasicDeliverEventArgs deliverEventArgs)
    {
        if (_subscriptionInfo.AutoAck)
            return false;
        var redeliveryCount = GetRedeliveryCount(deliverEventArgs);
        var maxRetries = _subscriptionInfo.DeadLetterTopic?.MaxRetries ?? 0;
        if (maxRetries <= redeliveryCount)
        {
            _logger.LogInformation(
                $"Message from RabbitMQ exchange: {deliverEventArgs.Exchange}, queue: {_subscriptionInfo.SubscriptionName}, messageId: {deliverEventArgs.BasicProperties.MessageId}, routing key: {deliverEventArgs.RoutingKey}, will be sent to DLE {_subscriptionInfo.DeadLetterTopic.Name}. It was redelivered: {redeliveryCount} times.");
            return false;
        }

        return true;
    }

    private static long GetRedeliveryCount(BasicDeliverEventArgs deliverEventArgs)
    {
        if (deliverEventArgs.BasicProperties.Headers?.TryGetValue("x-delivery-count", out var redeliveredCount) == true)
            return (long) redeliveredCount;

        return 0;
    }
}