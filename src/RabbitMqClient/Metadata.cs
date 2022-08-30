using System;

namespace RabbitMqClient;

public readonly struct TopicInfo : IEquatable<TopicInfo>
{
    public TopicInfo(string topicName, bool durable = true, bool autoDelete = false)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentNullException(nameof(topicName));
        TopicName = topicName;
        Durable = durable;
        AutoDelete = autoDelete;
    }

    public string TopicName { get; }
    public bool Durable { get; }
    public bool AutoDelete { get; }

    public bool Equals(TopicInfo other)
    {
        return ReferenceEquals(this, other) ||
               TopicName == other.TopicName && Durable == other.Durable && AutoDelete == other.AutoDelete;
    }

    public override bool Equals(object? obj)
    {
        return obj is TopicInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TopicName, Durable, AutoDelete);
    }
}

public struct SubscriptionInfo
{
    public SubscriptionInfo(TopicInfo topic,
        string subscriptionName,
        ushort maxMessagesInParallel = 30,
        bool autoAck = false,
        bool temporary = false,
        uint retryCount = 0,
        DeadLetterTopicInfo? deadLetterTopic = null)
    {
        Topic = topic;
        SubscriptionName = string.IsNullOrWhiteSpace(subscriptionName)
            ? throw new ArgumentNullException(nameof(subscriptionName))
            : subscriptionName;
        AutoAck = autoAck;
        Temporary = temporary;
        RetryCount = retryCount;
        DeadLetterTopic = deadLetterTopic;
        MaxMessagesInParallel = maxMessagesInParallel;
    }

    public TopicInfo Topic { get; }
    public string SubscriptionName { get; }
    public ushort MaxMessagesInParallel { get; }
    public bool AutoAck { get; }
    public bool Temporary { get; }
    public uint RetryCount { get; }
    public DeadLetterTopicInfo? DeadLetterTopic { get; }
}

public class DeadLetterTopicInfo
{
    public DeadLetterTopicInfo(string name, ushort maxRetries)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;
        MaxRetries = maxRetries;
    }

    public string Name { get; }
    public ushort MaxRetries { get; }
}

internal static class Globals
{
    public const string ExchangeType = "direct";
    public const string GeneralRoutingKey = "general";
    public const string DeadLetterExchangeType = "fanout";
}