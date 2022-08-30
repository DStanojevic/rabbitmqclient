using RabbitMqClient;
using SubscriberTestApp.Internal.Repositories;
using TestAppCommon;

namespace SubscriberTestApp.Internal.Subscribers;

public interface IDelayedWorkMessageHandler : IMessageHandler<MqGenericMessage<DelayInfo>>
{
    string WorkerName { get;}
}

public class DelayedWorkMessageHandler : IDelayedWorkMessageHandler
{
    private string? _workerName;
    private ILogger<DelayedWorkMessageHandler> _logger;

    public DelayedWorkMessageHandler(ILogger<DelayedWorkMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IProcessingOutcome> HandleMessage(IMessageQueueSubscriber subscriber, 
                                                        IDictionary<string, object> headers, 
                                                        MqGenericMessage<DelayInfo> message,
                                                        CancellationToken cancellationToken)
    {
        WorkerName = subscriber.ClientName;
        using (ActiveTasksRepository.EnqueueActiveWorker(this))
        {
            using (_logger.BeginScope($"Enqueued {WorkerName} message processing for subscription {subscriber.Subscription.SubscriptionName}. Message delay: {message.Payload.DelaySeconds} seconds."))
            {
                await Task.Delay((int) message.Payload.DelaySeconds * 1000, cancellationToken);
                _logger.LogInformation($"Finished {WorkerName} message processing for subscription {subscriber.Subscription.SubscriptionName}. Message delay: {message.Payload.DelaySeconds} seconds.");
                return new AcknowledgedOutcome();
            }
        }
    }

    public string WorkerName
    {
        get => string.IsNullOrWhiteSpace(_workerName) ? throw new InvalidOperationException("Worker name is not specified."): _workerName;
        set => _workerName = value;
    }
}