using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMqClient.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace RabbitMqClient.UnitTests
{



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

        private class TestMessageAggregateHandler : MessageAggregatorQueueSubscriber<TestMessge, AggregateException, int>
        {
            public TestMessageAggregateHandler(
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
        //private Mock<ILogger<TestMessageAggregateHandler>> _loggerMock;
        private SubscriptionInfo subscriptionFake = new SubscriptionInfo(new TopicInfo("test topic"), "test subscription");

        [OneTimeSetUp]
        public void Setup()
        {
            _connectionFactoryMock = mockRepository.Create<IConnectionFactory>();
            _consumerFactory = new ConsumerFactory(mockRepository.Create<IServiceScopeFactory>().Object, mockRepository.Create<ILoggerFactory>().Object);
            //_loggerMock = mockRepository.Create<ILogger<TestMessageAggregateHandler>>();
        }

        private TestMessageAggregateHandler CreateInstance(int bufferTimeOutMilliseconds,int bufferLimitCountOfMesages, Func<IEnumerable<TestMessge>, CancellationToken, Task<IProcessingOutcome>> aggregateHandler)
            => new TestMessageAggregateHandler(
                _connectionFactoryMock.Object,
                _consumerFactory,
                mockRepository.Create<ILogger>().Object,
                "test_client",
                subscriptionFake,
                bufferTimeOutMilliseconds,
                bufferLimitCountOfMesages,
                aggregateHandler);


        private async Task EmitMessages(MessageQueueSubscriber<IMessageHandler<TestMessge>, TestMessge, AggregateException> subscriber, int countOfMessages, int countOfDifferentMessages)
        {
            var radngomGenerator = new Random();
            for (int i = 0; i < countOfMessages; i++)
            {

                var messageId =  radngomGenerator.Next(1, countOfDifferentMessages);

                var message = new TestMessge(messageId, Guid.NewGuid().ToString(), radngomGenerator.NextInt64(1, 1000));
                _ = subscriber.HandleMessage(
                    mockRepository.Create<IServiceProvider>().Object,
                    "test_exchange",
                    messageId.ToString(),
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
            var aggregatedMessages = new List<(int messageId, int count)>();
            Task<IProcessingOutcome> assertAggregate(IEnumerable<TestMessge> messages, CancellationToken cancellationToken)
            {
                var groupedMessges = messages.GroupBy(p => p.Id);
                var countOfGroups = groupedMessges.Count();
                foreach (var group in groupedMessges)
                {
                    aggregatedMessages.Add((group.Key, group.Count()));
                }
                
                if (countOfGroups == 1)
                    return Task.FromResult((IProcessingOutcome)new AcknowledgedOutcome());

                return Task.FromResult((IProcessingOutcome)new NegativelyAcknowledgedOutcome($"Group contains keys: {countOfGroups}"));
            }

            var messageAggregateConsumer = CreateInstance(10000, 100, assertAggregate);

            await EmitMessages(messageAggregateConsumer, 10000, 3);

            Assert.AreEqual(3, aggregatedMessages.Count());
            Assert.Contains(1, aggregatedMessages.Select(p => p.messageId).ToArray());
            Assert.Contains(2, aggregatedMessages.Select(p => p.messageId).ToArray());
            Assert.Contains(3, aggregatedMessages.Select(p => p.messageId).ToArray());
        }

    }
}
