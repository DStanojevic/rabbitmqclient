using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;

namespace RabbitMqClient;

public struct RabbitMqMessageData
{
    public RabbitMqMessageData(ulong deliveryTag,
                               string topicName,
                               string routingKey,
                               string body,
                               IDictionary<string, object> headers,
                               string? applicationId = null,
                               string? messageId = null,
                               string? contentEncoding = null,
                               string? contentType = null,
                               string? typeOfMessage = null,
                               string? expiration = null)
    {
        DeliveryTag = deliveryTag;
        TopicName = topicName;
        RoutingKey = routingKey;
        Body = body;
        Headers = TransformHeaders(headers);
        ApplicationId = applicationId;
        MessageId = messageId;
        ContentEncoding = contentEncoding;
        ContentType = contentType;
        TypeOfMessage = typeOfMessage;
        Expiration = expiration;
    }

    public ulong DeliveryTag { get; }
    public string TopicName { get; }
    public string RoutingKey { get; }
    public string Body { get; }
    public IDictionary<string, object> Headers { get; }
    public string? ApplicationId { get; }
    public string? MessageId { get; }
    public string? ContentEncoding { get; }
    public string? ContentType { get; }
    public string? TypeOfMessage { get; }
    public string? Expiration { get; }


    private static IDictionary<string, object> TransformHeaders(IDictionary<string, object> headers)
    {
        if (headers == null)
            return null;
        var elements = new List<KeyValuePair<string, object>>();
        foreach (var header in headers)
        {
            if(header.Value is IEnumerable<byte> byteCollectionValue)
                elements.Add(new KeyValuePair<string, object>(header.Key, Encoding.UTF8.GetString(byteCollectionValue.ToArray())));
            else
                elements.Add(header);
        }

        return new Dictionary<string, object>(elements);
    }
}