using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Reports.Processing;
using ExpenseHub.Api.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace ExpenseHub.Worker;

/// <summary>
/// Consumes report messages one at a time and acknowledges each only after the outcome was persisted.
/// A message is acknowledged when the report was completed or recorded as failed, when it was already finished
/// (duplicate), or when it cannot be understood. When persistence is not possible the message is returned to
/// the queue after a pause. If the worker dies before acknowledging, the broker redelivers the message.
/// </summary>
internal sealed partial class ReportConsumerService : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReportConsumerService> _logger;

    public ReportConsumerService(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ReportConsumerService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(_configuration["APP_VERSION"] ?? "local", _options.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is BrokerUnreachableException or AlreadyClosedException or OperationInterruptedException or TimeoutException)
            {
                LogBrokerUnavailable(exception);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    [LoggerMessage(EventId = 30, Level = LogLevel.Information, Message = "Report worker starting (version {Version}), queue {Queue}.")]
    private partial void LogStarting(string version, string queue);

    [LoggerMessage(EventId = 31, Level = LogLevel.Warning, Message = "The broker is unavailable; trying again shortly.")]
    private partial void LogBrokerUnavailable(Exception exception);

    [LoggerMessage(EventId = 32, Level = LogLevel.Warning, Message = "A message that is not a valid report message was discarded.")]
    private partial void LogInvalidMessage();

    [LoggerMessage(EventId = 33, Level = LogLevel.Information, Message = "Report {ReportId} delivered with outcome {Outcome}; message acknowledged: {Acknowledged}.")]
    private partial void LogDelivery(Guid reportId, ReportProcessingOutcome outcome, bool acknowledged);

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        using IConnection connection = RabbitMqConnections.Create(_options, "expensehub-worker");
        using IModel channel = connection.CreateModel();

        RabbitMqConnections.DeclareQueue(channel, _options.QueueName);

        // One unacknowledged message at a time: a message that is being processed is "Unacked" in the broker.
        channel.BasicQos(0, 1, false);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdown += (_, _) => closed.TrySetResult();

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += (_, delivery) => OnReceivedAsync(channel, delivery, stoppingToken);
        channel.BasicConsume(_options.QueueName, false, consumer);

        using CancellationTokenRegistration registration = stoppingToken.Register(() => closed.TrySetCanceled(stoppingToken));
        await closed.Task;
    }

    private async Task OnReceivedAsync(IModel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        Guid? reportId = ReportMessage.TryParse(delivery.Body.Span);
        if (reportId is null)
        {
            LogInvalidMessage();
            channel.BasicAck(delivery.DeliveryTag, false);

            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ReportProcessingService>();

        ReportProcessingOutcome outcome = await processor.HandleAsync(reportId.Value, stoppingToken);
        bool acknowledge = ReportProcessingService.ShouldAcknowledge(outcome);
        LogDelivery(reportId.Value, outcome, acknowledge);

        if (acknowledge)
        {
            // Only now, after the result or the failure was saved (or nothing needed saving), may the broker forget it.
            channel.BasicAck(delivery.DeliveryTag, false);

            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(_options.RedeliveryDelaySeconds), stoppingToken);
        channel.BasicNack(delivery.DeliveryTag, false, true);
    }
}
