using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Abstractions;

/// <summary>
/// Persistence port of report requests, shared by the web application and the worker.
/// </summary>
internal interface IReportRepository
{
    Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists, newest first, only the requests created by the given user.
    /// </summary>
    Task<IReadOnlyList<ExpenseReport>> ListByRequesterAsync(string requesterId, CancellationToken cancellationToken);

    void Add(ExpenseReport report);

    /// <summary>
    /// Persists every staged change atomically. Throws <see cref="StateConflictException"/>
    /// when the request was modified concurrently.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
