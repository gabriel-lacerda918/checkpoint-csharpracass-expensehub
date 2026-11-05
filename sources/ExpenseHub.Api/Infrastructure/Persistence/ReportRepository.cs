using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Infrastructure.Persistence;

internal sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.ExpenseReports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseReport>> ListByRequesterAsync(string requesterId, CancellationToken cancellationToken)
    {
        return await _context.ExpenseReports
            .AsNoTracking()
            .Where(report => report.RequesterId == requesterId)
            .OrderByDescending(report => report.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Add(ExpenseReport report)
    {
        _context.ExpenseReports.Add(report);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // One SaveChanges is one transaction: the status, the result and the timestamps change together.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StateConflictException("The report was changed by another process. Reload it and try again.", exception);
        }
    }
}
