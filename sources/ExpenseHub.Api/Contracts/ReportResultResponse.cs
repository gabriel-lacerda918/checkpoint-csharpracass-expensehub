using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Contracts;

/// <summary>
/// Result of a completed report. It is saved when the report completes and is never recalculated.
/// </summary>
/// <param name="GeneratedAt">Instant in UTC when the worker computed the result.</param>
/// <param name="TotalCount">Total number of expenses in the period and scope.</param>
/// <param name="TotalAmount">Total amount of those expenses.</param>
/// <param name="ByStatus">The five expense statuses, including those with zero.</param>
public sealed record ReportResultResponse(
    DateTime GeneratedAt,
    int TotalCount,
    decimal TotalAmount,
    IReadOnlyList<StatusSummaryResponse> ByStatus);
