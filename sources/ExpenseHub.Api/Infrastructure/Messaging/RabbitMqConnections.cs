using System;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace ExpenseHub.Api.Infrastructure.Messaging;

internal static class RabbitMqConnections
{
    public static IConnection Create(RabbitMqOptions options, string clientName)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.UserName,
            Password = options.Password,
            ClientProvidedName = clientName,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
        };

        return factory.CreateConnection();
    }

    /// <summary>
    /// Declares the durable queue. Declaring is idempotent, so both processes do it before using the queue.
    /// </summary>
    public static void DeclareQueue(IModel channel, string queueName)
    {
        channel.QueueDeclare(queueName, true, false, false, new Dictionary<string, object>());
    }
}
