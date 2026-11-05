using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// A request for a refund summary of a period. The requester, scope and period are fixed when the request is
/// created; the worker later fills in the result (or the error) exactly once.
/// </summary>
internal sealed class ExpenseReport
{
    /// <summary>
    /// Processing attempts allowed before the request is marked as failed, so a poison message cannot loop forever.
    /// </summary>
    public const int MaxAttempts = 5;

    private ExpenseReport()
    {
        RequesterId = string.Empty;
    }

    public Guid Id { get; private set; }

    public string RequesterId { get; private set; }

    public ReportScope Scope { get; private set; }

    public DateOnly FromDate { get; private set; }

    public DateOnly ToDate { get; private set; }

    public ReportStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>
    /// Serialized result, written together with the Completed status and never recalculated.
    /// </summary>
    public string? ResultJson { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// How many times a worker started processing this request.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, incremented on every change.
    /// </summary>
    public int Version { get; private set; }

    public bool IsFinished => Status is ReportStatus.Completed or ReportStatus.Failed;

    public bool HasExhaustedAttempts => Attempts >= MaxAttempts;

    public static ExpenseReport Create(
        string requesterId,
        ReportScope scope,
        DateOnly fromDate,
        DateOnly toDate,
        DateTime nowUtc)
    {
        return new ExpenseReport
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            Scope = scope,
            FromDate = fromDate,
            ToDate = toDate,
            Status = ReportStatus.Pending,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// Starts (or resumes) processing. A request already in Processing, for example because a previous worker
    /// stopped before finishing, is picked up again; a finished request is never reprocessed.
    /// </summary>
    public void BeginProcessing(DateTime nowUtc)
    {
        EnsureNotFinished("start processing");

        Status = ReportStatus.Processing;
        StartedAtUtc ??= nowUtc;
        Attempts++;
        Version++;
    }

    public void Complete(string resultJson, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);

        if (Status != ReportStatus.Processing)
        {
            throw new StateConflictException(
                FormattableString.Invariant($"Cannot complete a report in status {Status}; expected status {ReportStatus.Processing}."));
        }

        ResultJson = resultJson;
        Status = ReportStatus.Completed;
        CompletedAtUtc = nowUtc;
        Version++;
    }

    public void Fail(string code, string message, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        EnsureNotFinished("fail");

        ErrorCode = code;
        ErrorMessage = message;
        Status = ReportStatus.Failed;
        CompletedAtUtc = nowUtc;
        Version++;
    }

    private void EnsureNotFinished(string operation)
    {
        if (IsFinished)
        {
            throw new StateConflictException(
                FormattableString.Invariant($"Cannot {operation} a report in status {Status}."));
        }
    }
}
