using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Application.Reports.Processing;

/// <summary>
/// Worker side of the report flow. It never throws for a delivery: the returned outcome tells the consumer
/// whether the message may be acknowledged. The result and the Completed status are saved in one write, and
/// the message is only acknowledged after that write succeeded.
/// <para>
/// Idempotency lives in the database: a finished request is never recalculated, even after the worker restarts
/// or the same message is delivered again. A request left in Processing by a worker that stopped is resumed
/// by the next delivery; the calculation may run twice, but only one result is ever stored.
/// </para>
/// </summary>
internal sealed partial class ReportProcessingService
{
    private readonly IReportRepository _repository;
    private readonly IReportDataSource _dataSource;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReportProcessingService> _logger;

    public ReportProcessingService(
        IReportRepository repository,
        IReportDataSource dataSource,
        TimeProvider timeProvider,
        ILogger<ReportProcessingService> logger)
    {
        _repository = repository;
        _dataSource = dataSource;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public static bool ShouldAcknowledge(ReportProcessingOutcome outcome)
    {
        return outcome != ReportProcessingOutcome.Retry;
    }

    public async Task<ReportProcessingOutcome> HandleAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            return await ProcessAsync(reportId, cancellationToken);
        }
        catch (StateConflictException exception)
        {
            LogConcurrentChange(exception, reportId);

            return ReportProcessingOutcome.Retry;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogTransientFailure(exception, reportId);

            return ReportProcessingOutcome.Retry;
        }
    }

    private async Task<ReportProcessingOutcome> ProcessAsync(Guid reportId, CancellationToken cancellationToken)
    {
        ExpenseReport? report = await _repository.FindAsync(reportId, cancellationToken);
        if (report is null)
        {
            LogMissing(reportId);

            return ReportProcessingOutcome.Discarded;
        }

        if (report.IsFinished)
        {
            LogDuplicate(reportId, report.Status);

            return ReportProcessingOutcome.Duplicate;
        }

        if (report.HasExhaustedAttempts)
        {
            int attempts = report.Attempts;
            report.Fail("MAX_ATTEMPTS_EXCEEDED", "The report could not be processed after several attempts.", Now());
            await _repository.SaveChangesAsync(cancellationToken);
            LogExhausted(reportId, attempts);

            return ReportProcessingOutcome.Failed;
        }

        report.BeginProcessing(Now());
        int attempt = report.Attempts;
        await _repository.SaveChangesAsync(cancellationToken);
        LogStarted(reportId, attempt);

        IReadOnlyList<ExpenseFigure> figures = await _dataSource.ListFiguresAsync(ReportScopeFilter.For(report), cancellationToken);
        DateTime finishedAt = Now();
        ReportResultResponse result = ReportCalculator.Summarize(figures, finishedAt);

        report.Complete(ReportResultSerializer.Serialize(result), finishedAt);
        await _repository.SaveChangesAsync(cancellationToken);
        LogCompleted(reportId, result.TotalCount);

        return ReportProcessingOutcome.Completed;
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Report {ReportId} processing started (attempt {Attempt}).")]
    private partial void LogStarted(Guid reportId, int attempt);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Report {ReportId} completed with {TotalCount} expenses.")]
    private partial void LogCompleted(Guid reportId, int totalCount);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Report {ReportId} is already {Status}; duplicate delivery ignored without any new effect.")]
    private partial void LogDuplicate(Guid reportId, ReportStatus status);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "Report {ReportId} does not exist; the message was discarded.")]
    private partial void LogMissing(Guid reportId);

    [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "Report {ReportId} failed after {Attempts} attempts.")]
    private partial void LogExhausted(Guid reportId, int attempts);

    [LoggerMessage(EventId = 15, Level = LogLevel.Warning, Message = "Report {ReportId} was changed concurrently; the message will be redelivered.")]
    private partial void LogConcurrentChange(Exception exception, Guid reportId);

    [LoggerMessage(EventId = 16, Level = LogLevel.Error, Message = "Report {ReportId} could not be processed now; the message will be redelivered.")]
    private partial void LogTransientFailure(Exception exception, Guid reportId);

    private DateTime Now()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
