namespace ExpenseHub.Api.Application.Reports.Processing;

/// <summary>
/// What happened to one delivery of a report message, which decides whether the broker may forget it.
/// </summary>
internal enum ReportProcessingOutcome
{
    /// <summary>The result was calculated and saved. Acknowledge.</summary>
    Completed = 0,

    /// <summary>The request was already finished: no new effect. Acknowledge.</summary>
    Duplicate = 1,

    /// <summary>The request was recorded as Failed (definitive). Acknowledge, do not redeliver.</summary>
    Failed = 2,

    /// <summary>There is no such request, so redelivering would never help. Acknowledge.</summary>
    Discarded = 3,

    /// <summary>Something transient prevented saving. Do not acknowledge as if it had been persisted.</summary>
    Retry = 4,
}
