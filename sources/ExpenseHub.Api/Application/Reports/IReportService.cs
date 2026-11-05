using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// Report use cases of the web application: register the request, follow it and read what the worker produced.
/// </summary>
public interface IReportService
{
    /// <summary>Registers a request and queues it for the worker. Never waits for the calculation.</summary>
    /// <param name="user">Authenticated caller, which becomes the owner of the request.</param>
    /// <param name="input">Period and scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered request in the Pending status.</returns>
    Task<ReportResponse> RequestAsync(CurrentUser user, ReportInput input, CancellationToken cancellationToken);

    /// <summary>Lists only the requests created by the caller.</summary>
    /// <param name="user">Authenticated caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The caller's requests, newest first.</returns>
    Task<IReadOnlyList<ReportResponse>> ListAsync(CurrentUser user, CancellationToken cancellationToken);

    /// <summary>Gets the progress and result of a request created by the caller.</summary>
    /// <param name="user">Authenticated caller.</param>
    /// <param name="id">Report identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request.</returns>
    Task<ReportResponse> GetAsync(CurrentUser user, Guid id, CancellationToken cancellationToken);

    /// <summary>Builds the Excel file from the persisted result of a completed request created by the caller.</summary>
    /// <param name="user">Authenticated caller.</param>
    /// <param name="id">Report identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The .xlsx file.</returns>
    Task<ReportFile> GetExcelAsync(CurrentUser user, Guid id, CancellationToken cancellationToken);
}
