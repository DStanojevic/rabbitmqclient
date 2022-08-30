# Overvirview

This library is a wrapper for for the [RabbitMQ .NET/C# client](https://www.rabbitmq.com/dotnet.html).  
Official RabbitMQ .NET client is a 'low level' API, so we builded our own 'helper library' to simplify interaction with the Rabbit MQ message broker.

This package is available at https://gitlab.nil.rs/publicnuget/nugetregistry/-/packages/ (visit https://gitlab.nil.rs/publicnuget/nugetregistry/-/blob/master/README.md to see how to use this package registry).


#### **Usage scenarios**:  https://gitlab.nil.rs/jafin2k/dtm/-/issues/109


# Usage examples

## Publishing

 - Configuration setup:

 ```json
   "RabbitMq": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest",
    "ClientName": "app:SenderTestApp"
  }
 ```

 - Services registration:

 ```C#
 //RabbitMqHelper message publisher.
builder.Services.RegisterMessagePublisher(builder.Configuration);
 ```

 - Inject ``` IMessageQueuePublisher ``` dependecy via constructor.

 - Code snippet that publish message:
 ```C#
    public async Task<string> PublishMessage(SendMessageRequest sendMessageRequest)
    {
        var topicInfo = new TopicInfo(sendMessageRequest.Header.TopicName);
        var message = CreateMessage(sendMessageRequest);
        await _messageQueuePublisher.SendMessageAsync(topicInfo, message);
        return message.Attributes.MessageId;
    }
 ```

## Subscribing and handling messages

- Configuration setup:

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest"
  },
  "RabbitMqSubscriptions": {
    "Subscriptions": [
      {
        "TopicInfo": {
          "TopicName": "TestTopic"
        },
        "Name": "TestTopic-Subscription1",
        "DeadLetterConfig": {
          "DeadLetterTopicName": "TestTopic-DLE"
        }
      },
      {
        "TopicInfo": {
          "TopicName": "WorkerTestTopic1"
        },
        "Name": "WorkerTestTopic1-Subscription1",
        "DeadLetterConfig": {
          "DeadLetterTopicName": "WorkerTestTopic1-DLE"
        }
      },
      ...
```

- Services registration

```C#
//RabbitMQHelper services registration.
builder.Services.RegisterMessageSubscriber(builder.Configuration);
```

- Implement ```IMessageHandler``` interface.  
Below is the code sample of implementation:
```C#
//This is just marker interface which use as identifier at service locator.
public interface ISimpleMessageHandler : IMessageHandler<MqGenericMessage<string>>
{
}

public class SimpleMessageHandler :  ISimpleMessageHandler
{
    private readonly IMessagesRepository _messagesRepository;
    private readonly ILogger<SimpleMessageHandler> _logger;
    public SimpleMessageHandler(IMessagesRepository messagesRepository, ILogger<SimpleMessageHandler> logger)
    {
        _messagesRepository = messagesRepository;
        _logger = logger;
    }

    public async Task<IProcessingOutcome> HandleMessage(IMessageQueueSubscriber subscriber,
                                                        IDictionary<string, object> headers, 
                                                        MqGenericMessage<string> message,
                                                        CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(
                   $"Processing message with id {message.Attributes.MessageId}. Topic: {subscriber.Subscription.Topic.TopicName}, Subscription: {subscriber.Subscription.SubscriptionName}."))
        {
            await _messagesRepository.AddMessage(message);
            _logger.LogInformation($"Added message with id {message.Attributes.MessageId}");
            return new AcknowledgedOutcome();
        }
    }
}
```
It is also necessary to register as dependency:
```C#
builder.Services.AddScoped<ISimpleMessageHandler, SimpleMessageHandler>();
```

- Implement ```AbstractSubscriberHostedService``` hosted service abstract class.

```C#
public class TestTopicSubscriberBackgroundService : AbstractSubscriberHostedService<ISimpleMessageHandler, MqGenericMessage<string>, Exception>
{
    public TestTopicSubscriberBackgroundService(IConnectionFactory connectionFactory, 
                                                IConsumerFactory consumerFactory, 
                                                ILogger<TestTopicSubscriberBackgroundService> logger, 
                                                RabbitMqSubscriptionsConfiguration configuration) 
        : base(connectionFactory, consumerFactory, logger, SubscriberName, configuration.GetSubscriptionInfo(SubscriptionName))
    {
    }

    public const string SubscriberName = "TestTopicSubscriber";

    public const string SubscriptionName = "TestTopic-Subscription1";
}
```
And registration at startup:
```C#
builder.Services.AddHostedService<TestTopicSubscriberBackgroundService>();
```