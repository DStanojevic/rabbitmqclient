namespace SenderTestApp.Models;


public class MessageHeader
{
    public string TopicName { get; set; }
    public string? MessageId { get; set; }
    public DateTime? MessageTime { get; set; }
}

public class SendMessageRequest
{
    public const string DefaultBodyType = "System.String";
    public MessageHeader Header { get; set; }
    public string BodyTypeName { get; set; } = DefaultBodyType;
    public string Body { get; set; }
}