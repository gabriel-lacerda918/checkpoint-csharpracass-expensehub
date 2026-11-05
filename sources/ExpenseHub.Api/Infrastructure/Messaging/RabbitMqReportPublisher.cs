using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Application.Reports.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ExpenseHub.Api.Infrastructure.Messaging;

/// <summary>
/// Publishes report messages to a durable queue as persistent messages using publisher confirms.
/// It returns only after the broker confirmed the message; anything else is reported as a
/// <see cref="ReportPublishException"/>, which the API turns into a 503 instead of a false 202.
/// </summary>
internal sealed partial class RabbitMqReportPublisher : IReportPublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqReportPublisher> _logger;
    private readonly object _gate = new();
    private IConnection? _connection;

    public RabbitMqReportPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqReportPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            Publish(reportId);

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            LogPublishFailed(exception, reportId);

            throw new ReportPublishException(reportId, exception);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    [LoggerMessage(EventId = 20, Level = LogLevel.Error, Message = "The broker did not confirm the message of report {ReportId}.")]
    private partial void LogPublishFailed(Exception exception, Guid reportId);

    private void Publish(Guid reportId)
    {
        IConnection connection = GetConnection();

        // A channel per publication keeps the code thread-safe; the connection is shared.
        using IModel channel = connection.CreateModel();
        channel.ConfirmSelect();
        RabbitMqConnections.DeclareQueue(channel, _options.QueueName);

        IBasicProperties properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(string.Empty, _options.QueueName, false, properties, ReportMessage.Serialize(reportId));
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(_options.PublishConfirmTimeoutSeconds));
    }

    private IConnection GetConnection()
    {
        lock (_gate)
        {
            if (_connection is null || !_connection.IsOpen)
            {
                _connection?.Dispose();
                _connection = RabbitMqConnections.Create(_options, "expensehub-web");
            }

            return _connection;
        }
    }
}
