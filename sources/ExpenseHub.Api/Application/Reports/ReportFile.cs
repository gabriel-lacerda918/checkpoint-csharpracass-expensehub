namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// A file produced from a persisted report result.
/// </summary>
/// <param name="FileName">Suggested file name, for example report-{id}.xlsx.</param>
/// <param name="ContentType">Media type of the content.</param>
/// <param name="Content">File bytes.</param>
public sealed record ReportFile(string FileName, string ContentType, byte[] Content);
