using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMqClient.Configuration;
using RabbitMqClient.Internal;

namespace RabbitMqClient;

public static class ServiceCollectionExtensions
{
    public static void RegisterMessagePublisher(this IServiceCollection serviceCollection, IConfiguration conf)
    {
        serviceCollection.RegisterRabbitMqConfiguration(conf);
        serviceCollection.AddTransient(GetConnectionFactory);
        serviceCollection.AddSingleton<IRabbitMqChannelWrapper, RabbitMqChannelWrapper>();
        serviceCollection.AddScoped<IMessageQueuePublisher, MessageQueuePublisher>();
        serviceCollection.AddScoped<IMessageQueueRepository, MessageQueueRepository>();
    }

    public static void RegisterMessageSubscriber(this IServiceCollection serviceCollection, IConfiguration conf,
        bool registerSubscriptionsConfiguration = true)
    {
        serviceCollection.RegisterRabbitMqConfiguration(conf);
        if (registerSubscriptionsConfiguration)
            serviceCollection.RegisterRabbitMqSubscriptionsConfiguration(conf);
        serviceCollection.AddTransient(GetConnectionFactory);
        serviceCollection.AddTransient<IConsumerFactory, ConsumerFactory>();
        serviceCollection.AddScoped<IMessageQueueRepository, MessageQueueRepository>();
    }

    private static RabbitMqConfiguration GetRabbitMqConfiguration(IServiceProvider serviceProvider)
    {
        var config = serviceProvider.GetService<IOptions<RabbitMqConfiguration>>().Value;
        config.Validate();
        return config;
    }

    public static void RegisterRabbitMqConfiguration(this IServiceCollection serviceCollection, IConfiguration conf)
    {
        var section = conf.GetRequiredSection("RabbitMq");
        serviceCollection.Configure<RabbitMqConfiguration>(section);
        serviceCollection.AddSingleton(GetRabbitMqConfiguration);
    }

    private static IConnectionFactory GetConnectionFactory(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetService<RabbitMqConfiguration>();
        var connectionFactory = new ConnectionFactory
        {
            HostName = configuration.HostName,
            UserName = configuration.UserName,
            Password = configuration.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
        if (!string.IsNullOrEmpty(configuration.ClientName))
            connectionFactory.ClientProvidedName = configuration.ClientName;
        return connectionFactory;
    }
}