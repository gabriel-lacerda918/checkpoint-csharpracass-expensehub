using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Application.Reports.Excel;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// The web side of the report flow: it registers the request and queues it, but never calculates anything.
/// Queries return what the worker saved.
/// </summary>
internal sealed partial class ReportService : IReportService
{
    private readonly IReportRepository _repository;
    private readonly IReportPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        IReportRepository repository,
        IReportPublisher publisher,
        TimeProvider timeProvider,
        ILogger<ReportService> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ReportResponse> RequestAsync(CurrentUser user, ReportInput input, CancellationToken cancellationToken)
    {
        ReportAccessPolicy.EnsureCanUseReports(user);
        ReportScope scope = ReportAccessPolicy.AuthorizeScope(user, input.Scope);
        (DateOnly from, DateOnly to) = ReportPeriod.Validate(input.FromDate, input.ToDate);

        var report = ExpenseReport.Create(user.UserId, scope, from, to, _timeProvider.GetUtcNow().UtcDateTime);

        // Nothing is queued before the request is stored: the worker reads everything from the stored record.
        Guid reportId = report.Id;
        _repository.Add(report);
        await _repository.SaveChangesAsync(cancellationToken);
        LogRegistered(reportId, scope);

        await _publisher.PublishAsync(reportId, cancellationToken);
        LogPublished(reportId);

        return ReportMapper.ToResponse(report);
    }

    public async Task<IReadOnlyList<ReportResponse>> ListAsync(CurrentUser user, CancellationToken cancellationToken)
    {
        ReportAccessPolicy.EnsureCanUseReports(user);

        IReadOnlyList<ExpenseReport> reports = await _repository.ListByRequesterAsync(user.UserId, cancellationToken);

        return reports.Select(ReportMapper.ToResponse).ToList();
    }

    public async Task<ReportResponse> GetAsync(CurrentUser user, Guid id, CancellationToken cancellationToken)
    {
        ExpenseReport report = await LoadOwnAsync(user, id, cancellationToken);

        return ReportMapper.ToResponse(report);
    }

    public async Task<ReportFile> GetExcelAsync(CurrentUser user, Guid id, CancellationToken cancellationToken)
    {
        ExpenseReport report = await LoadOwnAsync(user, id, cancellationToken);

        if (report.Status != ReportStatus.Completed || report.ResultJson is null)
        {
            throw new StateConflictException(
                FormattableString.Invariant($"The report is {report.Status}; the Excel file is only available when it is Completed."));
        }

        ReportResultResponse result = ReportResultSerializer.Deserialize(report.ResultJson);
        byte[] content = ReportExcelBuilder.Build(report.Id, report.Scope, report.FromDate, report.ToDate, result);

        return new ReportFile(
            "report-" + report.Id.ToString("D", CultureInfo.InvariantCulture) + ".xlsx",
            ReportExcelBuilder.ContentType,
            content);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Report {ReportId} registered with scope {Scope}.")]
    private partial void LogRegistered(Guid reportId, ReportScope scope);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Report {ReportId} published and confirmed by the broker.")]
    private partial void LogPublished(Guid reportId);

    private async Task<ExpenseReport> LoadOwnAsync(CurrentUser user, Guid id, CancellationToken cancellationToken)
    {
        ReportAccessPolicy.EnsureCanUseReports(user);

        ExpenseReport? report = await _repository.FindAsync(id, cancellationToken);
        if (report is null)
        {
            throw new NotFoundException("Report not found.");
        }

        ReportAccessPolicy.EnsureOwner(user, report);

        return report;
    }
}
