using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMqClient.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace RabbitMqClient.UnitTests
{


    public class TestConsoleLoger : ILogger, IDisposable
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            Console.WriteLine($"Begin scope: {state}");
            return this;
        }

        public void Dispose()
        {
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"Log level: {logLevel}\nEventId: {eventId}\nState: {state}\nException: {exception}\n");
        }
    }
    public class MessageAggregatorQueueSubscriberTests
    {
        private struct TestMessge
        {
            public TestMessge(int id, string name, long amount)
            {
                Id = id;
                Name = name;
                Amount = amount;
            }
            public int Id { get; set; }

            public string Name { get; set; }

            public long Amount { get; set; }
        }

        private class AggregateException : Exception
        {

        }

        private class TestMessageAggregateSubscriber : MessageAggregatorQueueSubscriber<TestMessge, AggregateException, int>
        {
            public TestMessageAggregateSubscriber(
                IConnectionFactory connectionFactory, 
                IConsumerFactory consumerFactory, 
                ILogger logger, 
                string clientName, 
                SubscriptionInfo subscriptionInfo, 
                int bufferTimeOutMilliseconds, 
                int bufferLimitCountOfMesages,
                Func<IEnumerable<TestMessge>, CancellationToken, Task<IProcessingOutcome>> aggregateHandler
                ) 
                : base(connectionFactory, consumerFactory, logger, clientName, subscriptionInfo, bufferTimeOutMilliseconds, bufferLimitCountOfMesages)
            {
                _aggregateHandler = aggregateHandler;
            }

            private readonly IList<IProcessingOutcome> processingOutcomes = new List<IProcessingOutcome>();

            public IEnumerable<IProcessingOutcome> ProcessingOutcomes => processingOutcomes;

            private readonly Func<IEnumerable<TestMessge>, CancellationToken, Task<IProcessingOutcome>> _aggregateHandler;

            protected override int GropingKeyPredicate(TestMessge message) => message.Id;

            protected async override Task<IProcessingOutcome> HangleMessagesBatch(IEnumerable<TestMessge> messages, CancellationToken cancellationToken)
            {
                var result = await _aggregateHandler(messages, cancellationToken);
                processingOutcomes.Add(result);
                return result;
            }
        }

        private MockRepository mockRepository = new MockRepository(MockBehavior.Default);
        private Mock<IConnectionFactory> _connectionFactoryMock;
        private IConsumerFactory _consumerFactory;
        private ILogger _logger = new TestConsoleLoger();
        private SubscriptionInfo subscriptionFake = new SubscriptionInfo(new TopicInfo("test topic"), "test subscription");

        [OneTimeSetUp]
        public void Setup()
        {
            _connectionFactoryMock = mockRepository.Create<IConnectionFactory>();
            _consumerFactory = new ConsumerFactory(mockRepository.Create<IServiceScopeFactory>().Object, mockRepository.Create<ILoggerFactory>().Object);
            //_loggerMock = mockRepository.Create<ILogger<TestMessageAggregateHandler>>();
        }

        private TestMessageAggregateSubscriber CreateInstance(int bufferTimeOutMilliseconds,int bufferLimitCountOfMesages, Func<IEnumerable<TestMessge>, CancellationToken, Task<IProcessingOutcome>> aggregateHandler)
            => new TestMessageAggregateSubscriber(
                _connectionFactoryMock.Object,
                _consumerFactory,
                _logger,
                "test_client",
                subscriptionFake,
                bufferTimeOutMilliseconds,
                bufferLimitCountOfMesages,
                aggregateHandler);

        private static int GetNextMessageId(int maxId, int currentId)
        {
            if (currentId == maxId)
                return 1;
            return ++currentId;
        }
        private async Task EmitMessages(MessageQueueSubscriber<IMessageHandler<TestMessge>, TestMessge, AggregateException> subscriber, int countOfMessages, int countOfDifferentMessages)
        {
            var radngomGenerator = new Random();
            var currentId = countOfDifferentMessages;
            for (int i = 0; i < countOfMessages; i++)
            {

                currentId =  GetNextMessageId(countOfDifferentMessages, currentId);

                var message = new TestMessge(currentId, Guid.NewGuid().ToString(), radngomGenerator.NextInt64(1, 1000));
                _ = subscriber.HandleMessage(
                    mockRepository.Create<IServiceProvider>().Object,
                    "test_exchange",
                    currentId.ToString(),
                    null,
                    null,
                    message,
                    CancellationToken.None
                );
            }
            
        }

        [Test]
        public async Task TestBasicFlow()
        {
            var aggregatedMessages = new ConcurrentBag<(int messageId, int count)>();
            Task<IProcessingOutcome> assertAggregate(IEnumerable<TestMessge> messages, CancellationToken cancellationToken)
            {
                var groupedMessges = messages.GroupBy(p => p.Id);
                var countOfGroups = groupedMessges.Count();
                _logger.LogInformation($"Message groups: {countOfGroups}");
                
                foreach (var group in groupedMessges)
                {
                    _logger.LogInformation($"Group key: {group.Key}. Count in group: {group.Count()}");
                    aggregatedMessages.Add((group.Key, group.Count()));
                }
                
                if (countOfGroups == 1)
                    return Task.FromResult((IProcessingOutcome)new AcknowledgedOutcome());

                return Task.FromResult((IProcessingOutcome)new NegativelyAcknowledgedOutcome($"Group contains keys: {countOfGroups}"));
            }

            using var messageAggregateConsumer = CreateInstance(1000, 3, assertAggregate);

            await EmitMessages(messageAggregateConsumer, 9, 3);
            await Task.Delay(1200);
            Assert.AreEqual(3, aggregatedMessages.Count());
            Assert.Contains(1, aggregatedMessages.Select(p => p.messageId).ToArray());
            Assert.Contains(2, aggregatedMessages.Select(p => p.messageId).ToArray());
            Assert.Contains(3, aggregatedMessages.Select(p => p.messageId).ToArray());
        }

    }
}
