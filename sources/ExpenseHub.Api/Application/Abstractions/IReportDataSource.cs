using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Abstractions;

/// <summary>
/// Read-only access to the expenses a report summarizes.
/// </summary>
internal interface IReportDataSource
{
    /// <summary>
    /// Returns the status and amount of the expenses matching the filter. The data source must apply the
    /// filter itself, before materializing anything.
    /// </summary>
    Task<IReadOnlyList<ExpenseFigure>> ListFiguresAsync(
        Expression<Func<Expense, bool>> filter,
        CancellationToken cancellationToken);
}
