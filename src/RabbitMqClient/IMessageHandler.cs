using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMqClient;

public interface IMessageHandler<in TMessage>
{
    Task<IProcessingOutcome> HandleMessage(IMessageQueueSubscriber subscriber, IDictionary<string, object> headers, TMessage message, CancellationToken cancellationToken);
}