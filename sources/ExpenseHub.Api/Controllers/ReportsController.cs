using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Refund reports processed in the background. Every operation is limited to the requests created by the caller.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.ReportUsers)]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="reports">Report use cases.</param>
    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// Registers a report request and queues it. Returns 202 as soon as the broker confirmed the message.
    /// </summary>
    /// <param name="request">Period and scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request in the Pending status, with a Location header pointing to its detail.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReportResponse>> Create(CreateReportRequest request, CancellationToken cancellationToken)
    {
        var input = new ReportInput(request.FromDate, request.ToDate, request.Scope);
        ReportResponse report = await _reports.RequestAsync(User.ToCurrentUser(), input, cancellationToken);

        return AcceptedAtAction(nameof(Get), new { id = report.Id }, report);
    }

    /// <summary>
    /// Lists only the requests created by the caller.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The caller's requests, newest first.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReportResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportResponse>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<ReportResponse> reports = await _reports.ListAsync(User.ToCurrentUser(), cancellationToken);

        return Ok(reports);
    }

    /// <summary>
    /// Gets the progress and result of a request created by the caller. It never recalculates.
    /// </summary>
    /// <param name="id">Report identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The request.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        ReportResponse report = await _reports.GetAsync(User.ToCurrentUser(), id, cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Downloads the persisted result of a completed request created by the caller as an .xlsx file.
    /// </summary>
    /// <param name="id">Report identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The spreadsheet.</returns>
    [HttpGet("{id:guid}/excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excel(Guid id, CancellationToken cancellationToken)
    {
        ReportFile file = await _reports.GetExcelAsync(User.ToCurrentUser(), id, cancellationToken);

        return File(file.Content, file.ContentType, file.FileName);
    }
}
