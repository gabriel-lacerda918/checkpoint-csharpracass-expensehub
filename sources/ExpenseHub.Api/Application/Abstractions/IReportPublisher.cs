using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Application.Abstractions;

/// <summary>
/// Puts a report request on the queue read by the worker.
/// </summary>
internal interface IReportPublisher
{
    /// <summary>
    /// Publishes the message and returns only after the broker confirmed it. Throws
    /// <see cref="Exceptions.ReportPublishException"/> when the broker did not confirm.
    /// </summary>
    Task PublishAsync(Guid reportId, CancellationToken cancellationToken);
}
