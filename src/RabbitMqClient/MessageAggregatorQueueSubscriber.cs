using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMqClient
{
    public abstract class MessageAggregatorQueueSubscriber<TMessage, THandledException, TKey> : AbstractSubscriberHostedService<IMessageHandler<TMessage>, TMessage, THandledException> where THandledException : Exception
    {
        private readonly int _bufferTimeOutMilliseconds;
        private readonly int _bufferLimitCountOfMessages;
        private readonly ConcurrentDictionary<TKey, AggregatingBuffer> _aggregates = new ConcurrentDictionary<TKey, AggregatingBuffer>();
        protected MessageAggregatorQueueSubscriber(
            IConnectionFactory connectionFactory, 
            IConsumerFactory consumerFactory, 
            ILogger logger, 
            string clientName, 
            SubscriptionInfo subscriptionInfo,
            int bufferTimeOutMilliseconds,
            int bufferLimitCountOfMesages) 
            : base(connectionFactory, consumerFactory, logger, clientName, subscriptionInfo)
        {
            _bufferTimeOutMilliseconds = bufferTimeOutMilliseconds;
            _bufferLimitCountOfMessages = bufferLimitCountOfMesages;
        }

        protected abstract TKey GropingKeyPredicate(TMessage message);

        protected abstract Task<IProcessingOutcome> HangleMessagesBatch(IEnumerable<TMessage> messages, CancellationToken cancellationToken);

        private async Task<IProcessingOutcome> HandleMessage(TMessage message, CancellationToken cancellationToken)
        {
            var key = GropingKeyPredicate(message);
            var buffer = _aggregates.GetOrAdd(key, new AggregatingBuffer(key, _bufferTimeOutMilliseconds, _bufferLimitCountOfMessages, HandleBatchCallback, Logger, cancellationToken));
            Logger.LogDebug($"Buffer with key {buffer.GroupingKey} is waiting.");
            return await buffer.Wait(message, cancellationToken);
        }

        private async Task<IProcessingOutcome> HandleBatchCallback(AggregatingBuffer buffer, CancellationToken cancellationToken)
        {
            var outcome = await HangleMessagesBatch(buffer.Messages, cancellationToken);
            if(_aggregates.TryRemove(buffer.GroupingKey, out var removedBuffer))
            {
                Logger.LogDebug($"Removing buffer with key {removedBuffer.GroupingKey}");
                removedBuffer.Dispose();
            }
            else
            {
                Logger.LogError($"Failed to remove buffer with key {removedBuffer.GroupingKey}!");
                throw new InvalidOperationException("Unable to remove buffer ");
            }
            return outcome;
        }

        protected override IMessageHandler<TMessage> CreateMessageHandler(IServiceProvider serviceProvider) =>
            new BufferedMessageHadler(HandleMessage);


        protected override void Dispose(bool disposing)
        {
            foreach (var aggregate in _aggregates.Values)
                aggregate.Dispose();

            if (disposing)
            {
                _aggregates.Clear();
            }

            base.Dispose(disposing);
        }


        private class AggregatingBuffer : IDisposable
        {
            private readonly int _bufferLimitCountOfMessages;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly Func<AggregatingBuffer, CancellationToken, Task<IProcessingOutcome>> _aggreggateHandlerDelegate;
            private readonly ConcurrentBag<TMessage> _messages = new ConcurrentBag<TMessage>();
            private bool disposedValue;
            private IProcessingOutcome _processingOutcome;
            private ILogger _logger;

            public AggregatingBuffer(
                TKey groupingKey, 
                int bufferingTimeMilliseconds, 
                int bufferingLimitCountOfMessages, 
                Func<AggregatingBuffer, CancellationToken, Task<IProcessingOutcome>> aggreggateHandlerDelegate, 
                ILogger logger, 
                CancellationToken cancellationToken
                )
            {
                GroupingKey = groupingKey;
                _bufferLimitCountOfMessages = bufferingLimitCountOfMessages;
                _aggreggateHandlerDelegate = aggreggateHandlerDelegate;
                _logger = logger;
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, new CancellationTokenSource(bufferingTimeMilliseconds).Token);
            }

            public TKey GroupingKey { get; }
            public IEnumerable<TMessage> Messages => _messages;

            public async Task<IProcessingOutcome> Wait(TMessage message, CancellationToken cancellationToken)
            {
                _messages.Add(message);
                
                if(_messages.Count < _bufferLimitCountOfMessages)
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cts.Token);
                    }
                    catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
                    {
                        _logger.LogDebug($"Buffer timeout exceeded.");
                    }
                }
                else
                {
                    _logger.LogDebug($"Count of messages exceeded in buffer.");
                    _cancellationTokenSource.Cancel();
                }
                return await GetOutcome(cancellationToken);
            }


            private async Task<IProcessingOutcome> GetOutcome(CancellationToken cancellation)
            {
                
                if (_processingOutcome == null)
                {
                    //TODO: create unit test to make sure that this will be invoked only once.
                    _processingOutcome  = await _aggreggateHandlerDelegate(this, cancellation);
                    _logger.LogDebug($"Buffer group id: {GroupingKey}. New outcome: {_processingOutcome}. Disposed: {disposedValue}.");
                }
                else
                {
                    _logger.LogDebug($"Buffer group id: {GroupingKey}. Existing outcome: {_processingOutcome}. Disposed: {disposedValue}.");
                }
                return _processingOutcome;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        _messages.Clear();
                    }
                    _cancellationTokenSource.Dispose();
                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            ~AggregatingBuffer()
            {
                Dispose(disposing: false);
            }
        }

        private class BufferedMessageHadler : IMessageHandler<TMessage>
        {
            private readonly Func<TMessage, CancellationToken, Task<IProcessingOutcome>> _bufferingTask;
            public BufferedMessageHadler(Func<TMessage, CancellationToken, Task<IProcessingOutcome>> bufferingTask)
            {
                _bufferingTask = bufferingTask;
            }

            public async Task<IProcessingOutcome> HandleMessage(IMessageQueueSubscriber subscriber, IDictionary<string, object> headers, TMessage message, CancellationToken cancellationToken) =>
                await _bufferingTask(message, cancellationToken);

        }
    }
}
