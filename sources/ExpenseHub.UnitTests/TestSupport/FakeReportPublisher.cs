using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Application.Exceptions;

namespace ExpenseHub.UnitTests.TestSupport;

internal sealed class FakeReportPublisher : IReportPublisher
{
    public List<Guid> Published { get; } = new();

    /// <summary>
    /// When set, the publication is not confirmed by the "broker".
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Runs when a publication is attempted, to observe the state of the system at that moment.
    /// </summary>
    public Action<Guid>? OnPublish { get; set; }

    public Task PublishAsync(Guid reportId, CancellationToken cancellationToken)
    {
        OnPublish?.Invoke(reportId);

        if (Failure is not null)
        {
            throw new ReportPublishException(reportId, Failure);
        }

        Published.Add(reportId);

        return Task.CompletedTask;
    }
}
