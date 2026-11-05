using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Infrastructure.Persistence;

internal sealed class ReportDataSource : IReportDataSource
{
    private readonly AppDbContext _context;

    public ReportDataSource(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExpenseFigure>> ListFiguresAsync(
        Expression<Func<Expense, bool>> filter,
        CancellationToken cancellationToken)
    {
        // The period and scope are applied by the database; only status and amount are read. The amounts are
        // summed with decimal arithmetic by the calculator because SQLite has no native decimal type.
        return await _context.Expenses
            .AsNoTracking()
            .Where(filter)
            .Select(expense => new ExpenseFigure(expense.Status, expense.Amount))
            .ToListAsync(cancellationToken);
    }
}
