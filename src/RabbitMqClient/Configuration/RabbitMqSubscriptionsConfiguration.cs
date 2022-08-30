using System;
using System.Collections.Generic;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RabbitMqClient.Configuration;

public class RabbitMqSubscriptionsConfiguration
{
    public List<SubscriptionConfiguration> Subscriptions { get; set; } = new List<SubscriptionConfiguration>();

    public void Validate()
    {
        var validator = new RabbitMqSubscriptionsConfigurationValidator();
        var result = validator.Validate(this);
        if (!result.IsValid)
            throw new Exception($"Invalid configuration: {result}");
        foreach (var subscriptionConfiguration in Subscriptions)
            subscriptionConfiguration.Validate();
    }
}


public class TopicConfiguration
{
    public string TopicName { get; set; }
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;

    public void Validate()
    {
        var validator = new TopicConfigurationValidator();
        var result = validator.Validate(this);
        if (!result.IsValid)
            throw new Exception($"Invalid configuration: {result}");
    }
}

public class DeadLetterConfiguration
{
    public string DeadLetterTopicName { get; set; }
    public string MaxRetriesConfig { get; set; }
    public ushort MaxRetries => string.IsNullOrEmpty(MaxRetriesConfig) ? (ushort)0 : ushort.Parse(MaxRetriesConfig);

    public void Validate()
    {
        var validator = new DeadLetterConfigurationValidator();
        var result = validator.Validate(this);
        if (!result.IsValid)
            throw new Exception($"Invalid configuration: {result}");
    }
}

public class SubscriptionConfiguration
{
    public TopicConfiguration TopicInfo { get; set; }
    public string Name { get; set; }
    public ushort MaxMessagesInParallel => string.IsNullOrWhiteSpace(MaxMessagesInParallelConfig) ? (ushort)30 : ushort.Parse(MaxMessagesInParallelConfig);
    public string MaxMessagesInParallelConfig { get; set; }
    public bool AutoAck { get; set; } = false;
    public bool Temporary { get; set; } = false;

    public int RetryCount
    {
        get
        {
            if (string.IsNullOrEmpty(RetryCountConfig))
                return 0;
            return int.Parse(RetryCountConfig);
        }
    }

    public string RetryCountConfig { get; set; }
    public DeadLetterConfiguration? DeadLetterConfig { get; set; }

    public void Validate()
    {
        var validator = new SubscriptionConfigurationValidator();
        var result = validator.Validate(this);
        if (!result.IsValid)
            throw new Exception($"Invalid configuration: {result}");
        TopicInfo.Validate();
        DeadLetterConfig?.Validate();
    }
}

internal class RabbitMqSubscriptionsConfigurationValidator : AbstractValidator<RabbitMqSubscriptionsConfiguration>
{
    public RabbitMqSubscriptionsConfigurationValidator()
    {
        RuleFor(x => x.Subscriptions).NotEmpty();
    }
}

internal class SubscriptionConfigurationValidator : AbstractValidator<SubscriptionConfiguration>
{
    public SubscriptionConfigurationValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.TopicInfo).NotNull();
        RuleFor(x => x.RetryCountConfig).Must(val => string.IsNullOrEmpty(val) || uint.TryParse(val, out _))
            .WithMessage("RetryCountConfig must be 0 or positive integer.");
        RuleFor(x => x.MaxMessagesInParallelConfig)
            .Must(val => string.IsNullOrWhiteSpace(val) || ushort.TryParse(val, out _))
            .WithMessage("MaxMessagesInParallelConfig must be ushort");
    }
}

internal class TopicConfigurationValidator : AbstractValidator<TopicConfiguration>
{
    public TopicConfigurationValidator()
    {
        RuleFor(x => x.TopicName).NotEmpty();
    }
}

internal class DeadLetterConfigurationValidator : AbstractValidator<DeadLetterConfiguration>
{
    public DeadLetterConfigurationValidator()
    {
        RuleFor(x => x.DeadLetterTopicName).NotEmpty();
        RuleFor(x => x.MaxRetriesConfig).Must(c => string.IsNullOrEmpty(c) || ushort.TryParse(c, out _))
            .WithMessage("MaxRetriesConfig must be ushort.");
    }
}

public static class ServiceCollectionsExtensions
{
    private static RabbitMqSubscriptionsConfiguration GetConfiguration(IServiceProvider serviceProvider)
    {
        var config = serviceProvider.GetService<IOptions<RabbitMqSubscriptionsConfiguration>>().Value;
        config.Validate();
        return config;
    }

    public static void RegisterRabbitMqSubscriptionsConfiguration(this IServiceCollection serviceCollection, IConfiguration conf)
    {
        var subscriptionsConfigurationsSection = conf.GetSection("RabbitMqSubscriptions");
        serviceCollection.Configure<RabbitMqSubscriptionsConfiguration>(subscriptionsConfigurationsSection);
        serviceCollection.AddSingleton(GetConfiguration);
    }

}