using System;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// Data accepted to request a report. The requester is never part of it: it comes from the token.
/// </summary>
/// <param name="FromDate">First day of the period, inclusive.</param>
/// <param name="ToDate">Last day of the period, inclusive.</param>
/// <param name="Scope">Mine or Global.</param>
public sealed record ReportInput(DateOnly? FromDate, DateOnly? ToDate, string? Scope);
