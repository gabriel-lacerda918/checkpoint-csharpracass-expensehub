using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ connection and queue settings shared by the web application and the worker. The broker credentials
/// come from User Secrets or environment variables, never from a versioned file.
/// </summary>
internal sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string VirtualHost { get; set; } = "/";

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string QueueName { get; set; } = "expensehub.reports.requested";

    /// <summary>
    /// How long the web application waits for the broker to confirm a published message.
    /// </summary>
    [Range(1, 120)]
    public int PublishConfirmTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Pause before a message that could not be processed is returned to the queue, so a transient failure
    /// does not turn into a busy loop.
    /// </summary>
    [Range(1, 300)]
    public int RedeliveryDelaySeconds { get; set; } = 5;
}
