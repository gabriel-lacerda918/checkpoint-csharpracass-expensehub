using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// The only two facts of an expense a report needs, read at processing time.
/// </summary>
internal sealed record ExpenseFigure(ExpenseStatus Status, decimal Amount);
