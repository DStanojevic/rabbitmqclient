using System;
using System.Linq;

namespace RabbitMqClient.Configuration;

public static class ConfigurationHelpers
{
    public static SubscriptionInfo GetSubscriptionInfo(this SubscriptionConfiguration subscriptionConfiguration)
    {
        var topic = new TopicInfo(subscriptionConfiguration.TopicInfo.TopicName,
                                  subscriptionConfiguration.TopicInfo.Durable,
                                  subscriptionConfiguration.TopicInfo.AutoDelete);

        DeadLetterTopicInfo deadLetterTopic = null;
        if (subscriptionConfiguration.DeadLetterConfig != null)
        {
            deadLetterTopic = new DeadLetterTopicInfo(subscriptionConfiguration.DeadLetterConfig.DeadLetterTopicName,
                                                      subscriptionConfiguration.DeadLetterConfig.MaxRetries);
        }

        return new SubscriptionInfo(topic,
                                    subscriptionConfiguration.Name,
                                    subscriptionConfiguration.MaxMessagesInParallel,
                                    subscriptionConfiguration.AutoAck,
                                    subscriptionConfiguration.Temporary,
                                    (uint)subscriptionConfiguration.RetryCount,
                                    deadLetterTopic);
    }

    public static SubscriptionInfo GetSubscriptionInfo(this RabbitMqSubscriptionsConfiguration configuration, string subscriptionName)
    {
        var testTopicSubscriptionConfiguration = configuration.Subscriptions.First(p => p.Name.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
        return testTopicSubscriptionConfiguration.GetSubscriptionInfo();
    }
}