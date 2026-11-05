using System;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Reports;

internal static class ReportMapper
{
    public static ReportResponse ToResponse(ExpenseReport report)
    {
        return new ReportResponse(
            report.Id,
            report.Scope.ToString(),
            report.FromDate,
            report.ToDate,
            report.Status.ToString(),
            AsUtc(report.CreatedAtUtc),
            AsUtc(report.StartedAtUtc),
            AsUtc(report.CompletedAtUtc),
            report.ResultJson is null ? null : ReportResultSerializer.Deserialize(report.ResultJson),
            report.ErrorCode is null ? null : new ReportErrorResponse(report.ErrorCode, report.ErrorMessage ?? string.Empty));
    }

    private static DateTime AsUtc(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? AsUtc(DateTime? value)
    {
        return value is null ? null : AsUtc(value.Value);
    }
}
