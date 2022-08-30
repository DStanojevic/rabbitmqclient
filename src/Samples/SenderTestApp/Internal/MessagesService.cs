using System.Runtime.Serialization;
using System.Text.Json;
using RabbitMqClient;
using SenderTestApp.Models;
using TestAppCommon;

namespace SenderTestApp.Internal;

public interface IMessagesService
{
    Task<string> PublishMessage(SendMessageRequest sendMessageRequest);
    Task<IEnumerable<RabbitMqMessageData>> PollMessages(string queueName);
    Task<IEnumerable<string>> PublishMessages(IEnumerable<SendMessageRequest> sendMessageRequests);
}

public class MessagesService : IMessagesService
{
    private readonly IMessageQueuePublisher _messageQueuePublisher;
    private readonly IMessageQueueRepository _messageQueueRepository;

    public MessagesService(IMessageQueuePublisher messageQueuePublisher, IMessageQueueRepository messageQueueRepository)
    {
        _messageQueuePublisher = messageQueuePublisher;
        _messageQueueRepository = messageQueueRepository;
    }
    public async Task<string> PublishMessage(SendMessageRequest sendMessageRequest)
    {
        var topicInfo = new TopicInfo(sendMessageRequest.Header.TopicName);
        var message = CreateMessage(sendMessageRequest);
        await _messageQueuePublisher.SendMessageAsync(topicInfo, message);
        return message.Attributes.MessageId;
    }

    public async Task<IEnumerable<string>> PublishMessages(IEnumerable<SendMessageRequest> sendMessageRequests)
    {
        var topicGroups = sendMessageRequests.GroupBy(p => p.Header.TopicName);
        var messageIds = new List<string>();
        foreach (var topicGroup in topicGroups)
        {
            var topicInfo = new TopicInfo(topicGroup.Key);
            var messages = topicGroup.Select(CreateMessage);
            await _messageQueuePublisher.SendMessagesAsync(topicInfo, messages);
            messageIds.AddRange(messages.Select(p => p.Attributes.MessageId));
        }

        return messageIds;
    }

    public Task<IEnumerable<RabbitMqMessageData>> PollMessages(string queueName)
        => _messageQueueRepository.GetMessagesAsync(queueName);

    private static MqGenericMessage<object> CreateMessage(SendMessageRequest sendMessageRequest)
        => new()
        {
            Attributes = new MessageAttributes
            {
                MessageId = string.IsNullOrWhiteSpace(sendMessageRequest.Header.MessageId)
                    ? Guid.NewGuid().ToString()
                    : sendMessageRequest.Header.MessageId,
                Sender = "SenderTestApp",
                SendingTime = sendMessageRequest.Header.MessageTime ?? DateTime.Now
            },
            Payload = SendMessageRequest.DefaultBodyType.Equals(sendMessageRequest.BodyTypeName,
                StringComparison.Ordinal)
                ? sendMessageRequest.Body
                : JsonSerializer.Deserialize(sendMessageRequest.Body, Type.GetType(sendMessageRequest.BodyTypeName)) ?? throw new SerializationException("Failed to deserialize message body.")
        };
}