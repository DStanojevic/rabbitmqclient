namespace TestAppCommon;

public class MessageAttributes
{
    public string MessageId { get; set; }
    public DateTime SendingTime { get; set; }
    public string Sender { get; set; }
}

public abstract class MqMessageBase
{
    public MessageAttributes Attributes { get; set; }
}

public class MqGenericMessage<TPayload> : MqMessageBase
{
    public TPayload Payload { get; set; }
}