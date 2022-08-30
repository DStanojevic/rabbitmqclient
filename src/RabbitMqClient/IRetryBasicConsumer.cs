using System;
using RabbitMQ.Client;

namespace RabbitMqClient;

public interface IRetryBasicConsumer<THandledException> : IBasicConsumer where THandledException : Exception
{
}