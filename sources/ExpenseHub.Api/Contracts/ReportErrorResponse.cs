namespace ExpenseHub.Api.Contracts;

/// <summary>
/// Sanitized description of why a report failed.
/// </summary>
/// <param name="Code">Stable error code.</param>
/// <param name="Message">Human readable message without internal details.</param>
public sealed record ReportErrorResponse(string Code, string Message);
