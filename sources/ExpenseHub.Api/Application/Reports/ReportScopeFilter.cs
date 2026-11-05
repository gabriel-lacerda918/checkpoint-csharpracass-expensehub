using System;
using System.Linq.Expressions;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// Which expenses a report covers. The period applies to <see cref="Expense.ExpenseDate"/>, both ends included,
/// and Mine uses the requester stored on the server, never anything coming from a message.
/// </summary>
internal static class ReportScopeFilter
{
    public static Expression<Func<Expense, bool>> For(ExpenseReport report)
    {
        DateOnly from = report.FromDate;
        DateOnly to = report.ToDate;
        string requesterId = report.RequesterId;

        if (report.Scope == ReportScope.Global)
        {
            return expense => expense.ExpenseDate >= from && expense.ExpenseDate <= to;
        }

        return expense => expense.ExpenseDate >= from
            && expense.ExpenseDate <= to
            && expense.OwnerId == requesterId;
    }
}
