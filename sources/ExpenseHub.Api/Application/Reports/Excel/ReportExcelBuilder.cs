using System;
using System.Collections.Generic;
using System.Globalization;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Reports.Excel;

/// <summary>
/// Lays out a persisted report result as a worksheet. It only formats what was saved and never recalculates.
/// </summary>
internal static class ReportExcelBuilder
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(Guid reportId, ReportScope scope, DateOnly fromDate, DateOnly toDate, ReportResultResponse result)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[] { XlsxCell.FromHeader("Report ID"), XlsxCell.FromText(reportId.ToString("D", CultureInfo.InvariantCulture)) },
            new[] { XlsxCell.FromHeader("Scope"), XlsxCell.FromText(scope.ToString()) },
            new[] { XlsxCell.FromHeader("From"), XlsxCell.FromText(fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) },
            new[] { XlsxCell.FromHeader("To"), XlsxCell.FromText(toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) },
            new[] { XlsxCell.FromHeader("Generated at (UTC)"), XlsxCell.FromText(result.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)) },
            Array.Empty<XlsxCell>(),
            new[] { XlsxCell.FromHeader("Status"), XlsxCell.FromHeader("Count"), XlsxCell.FromHeader("Amount") },
        };

        foreach (StatusSummaryResponse item in result.ByStatus)
        {
            rows.Add(new[] { XlsxCell.FromText(item.Status), XlsxCell.FromInteger(item.Count), XlsxCell.FromMoney(item.Amount) });
        }

        rows.Add(new[] { XlsxCell.FromHeader("Total"), XlsxCell.FromInteger(result.TotalCount), XlsxCell.FromMoney(result.TotalAmount) });

        return XlsxWorkbookBuilder.Build("Report", rows);
    }
}
