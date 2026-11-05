using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// Pure summary of expenses by status. Decimal arithmetic only, so there is no floating point error.
/// </summary>
internal static class ReportCalculator
{
    public static ReportResultResponse Summarize(IEnumerable<ExpenseFigure> figures, DateTime generatedAtUtc)
    {
        ExpenseStatus[] statuses = Enum.GetValues<ExpenseStatus>();
        var counts = new Dictionary<ExpenseStatus, int>();
        var amounts = new Dictionary<ExpenseStatus, decimal>();

        foreach (ExpenseStatus status in statuses)
        {
            counts[status] = 0;
            amounts[status] = 0m;
        }

        foreach (ExpenseFigure figure in figures)
        {
            counts[figure.Status]++;
            amounts[figure.Status] += figure.Amount;
        }

        List<StatusSummaryResponse> byStatus = statuses
            .Select(status => new StatusSummaryResponse(status.ToString(), counts[status], amounts[status]))
            .ToList();

        return new ReportResultResponse(
            generatedAtUtc,
            byStatus.Sum(item => item.Count),
            byStatus.Sum(item => item.Amount),
            byStatus);
    }
}
