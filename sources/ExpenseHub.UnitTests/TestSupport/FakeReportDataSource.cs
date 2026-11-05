using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.TestSupport;

/// <summary>
/// Expense source for the report tests. It applies the filter it receives, exactly as the database would.
/// </summary>
internal sealed class FakeReportDataSource : IReportDataSource
{
    public List<Expense> Expenses { get; } = new();

    public int QueryCount { get; private set; }

    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<ExpenseFigure>> ListFiguresAsync(
        Expression<Func<Expense, bool>> filter,
        CancellationToken cancellationToken)
    {
        QueryCount++;

        if (Failure is not null)
        {
            throw Failure;
        }

        IReadOnlyList<ExpenseFigure> figures = Expenses
            .Where(filter.Compile())
            .Select(expense => new ExpenseFigure(expense.Status, expense.Amount))
            .ToList();

        return Task.FromResult(figures);
    }
}
