using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqClient.Internal;

namespace RabbitMqClient.UnitTests;

[TestFixture]
public class RetryAsyncConsumerTests
{
    [Test]
    public async Task HappyPath()
    {
        #region Arrange
        #region Mocking
        var mockRepository = new MockRepository(MockBehavior.Default);
        var channelMock = mockRepository.Create<IModel>();
        var serviceScopeFactoryMock = mockRepository.Create<IServiceScopeFactory>();
        var serviceScopeMock = mockRepository.Create<IServiceScope>();
        var serviceProvider = mockRepository.Create<IServiceProvider>();
        serviceScopeMock.SetupGet(p => p.ServiceProvider).Returns(serviceProvider.Object);
        serviceScopeFactoryMock.Setup(p => p.CreateScope()).Returns(serviceScopeMock.Object);
        var onMessageReceivedMock = mockRepository.Create<Func<IServiceProvider, BasicDeliverEventArgs, CancellationToken, Task<IProcessingOutcome>>>();
        onMessageReceivedMock
            .Setup(onRetry => onRetry(It.IsAny<IServiceProvider>(), It.IsAny<BasicDeliverEventArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcknowledgedOutcome());
        var loggerMock = mockRepository.Create<ILogger<IRetryBasicConsumer<Exception>>>();
        var onRetryMock = mockRepository.Create<Action<BasicDeliverEventArgs, Exception, int>>();

        var subscriptionInfoMock = new SubscriptionInfo(new TopicInfo("TestTopic"), "TestSubscription");
        #endregion Mocking


        var consumer = new RetryAsyncConsumer<Exception>(channelMock.Object,
                                                         serviceScopeFactoryMock.Object, 
                                                         subscriptionInfoMock, 
                                                         onMessageReceivedMock.Object,
                                                         onRetryMock.Object, 
                                                         loggerMock.Object, 
                                                         CancellationToken.None);
        var deliveryTag = (ulong) 10;
        #endregion

        #region Act
        consumer.HandleBasicDeliver(consumerTag:"test tag", 
                                    deliveryTag: deliveryTag, 
                                    redelivered:false, 
                                    exchange:subscriptionInfoMock.Topic.TopicName, 
                                    routingKey:string.Empty,
                                    properties:null,
                                    body:new ReadOnlyMemory<byte>());
        await Task.Delay(100);
        #endregion


        #region Assert
        onRetryMock.Verify(p => p(It.IsAny<BasicDeliverEventArgs>(), It.IsAny<Exception>(), It.IsAny<int>()), Times.Never);
        channelMock.Verify(m => m.BasicAck(It.Is<ulong>(p => p == deliveryTag), It.Is<bool>(v => v == false)), Times.Once);
        #endregion
    }

    [TestCase(2)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(25)]
    public async Task ItRetries2TimesThanPass(int retryCount)
    {
        var attempt = 0;
        Task<IProcessingOutcome> OnMessageReceived(IServiceProvider serviceProvider, BasicDeliverEventArgs deliverEventArgs, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref attempt);
            if(retryCount >= attempt)
                throw new TimeoutException("Mocking time out.");

            return Task.FromResult((IProcessingOutcome)new AcknowledgedOutcome());
        }

        #region Arrange
        #region Mocking
        var mockRepository = new MockRepository(MockBehavior.Default);
        var channelMock = mockRepository.Create<IModel>();
        var serviceScopeFactoryMock = mockRepository.Create<IServiceScopeFactory>();
        var serviceScopeMock = mockRepository.Create<IServiceScope>();
        var serviceProvider = mockRepository.Create<IServiceProvider>();
        serviceScopeMock.SetupGet(p => p.ServiceProvider).Returns(serviceProvider.Object);
        serviceScopeFactoryMock.Setup(p => p.CreateScope()).Returns(serviceScopeMock.Object);
        var loggerMock = mockRepository.Create<ILogger<IRetryBasicConsumer<TimeoutException>>>();
        var onRetryMock = mockRepository.Create<Action<BasicDeliverEventArgs, Exception, int>>();

        var subscriptionInfoMock = new SubscriptionInfo(new TopicInfo("TestTopic"), "TestSubscription",retryCount:(uint)retryCount);
        #endregion Mocking


        var consumer = new RetryAsyncConsumer<TimeoutException>(channelMock.Object,
                                                                serviceScopeFactoryMock.Object,
                                                                subscriptionInfoMock,
                                                                OnMessageReceived,
                                                                onRetryMock.Object,
                                                                loggerMock.Object,
                                                                CancellationToken.None);
        var deliveryTag = (ulong)10;
        #endregion

        #region Act
        consumer.HandleBasicDeliver(consumerTag: "test tag",
                                    deliveryTag: deliveryTag,
                                    redelivered: false,
                                    exchange: subscriptionInfoMock.Topic.TopicName,
                                    routingKey: string.Empty,
                                    properties: null,
                                    body: new ReadOnlyMemory<byte>());
        await Task.Delay(100);
        #endregion


        #region Assert
        onRetryMock.Verify(p => p(It.IsAny<BasicDeliverEventArgs>(), It.IsAny<TimeoutException>(), It.IsAny<int>()), Times.Exactly(retryCount));
        channelMock.Verify(m => m.BasicAck(It.Is<ulong>(p => p == deliveryTag), It.Is<bool>(v => v == false)), Times.Once);
        Assert.AreEqual(retryCount + 1, attempt);
        #endregion
    }
}