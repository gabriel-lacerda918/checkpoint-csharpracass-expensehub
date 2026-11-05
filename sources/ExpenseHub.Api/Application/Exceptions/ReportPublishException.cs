using System;

namespace ExpenseHub.Api.Application.Exceptions;

/// <summary>
/// The request was registered but the broker did not confirm the message (HTTP 503). The request stays
/// stored, so the caller can query it by id.
/// </summary>
internal sealed class ReportPublishException : Exception
{
    public ReportPublishException()
    {
    }

    public ReportPublishException(string message)
        : base(message)
    {
    }

    public ReportPublishException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ReportPublishException(Guid reportId, Exception innerException)
        : base("The report was registered but the broker did not confirm the message.", innerException)
    {
        ReportId = reportId;
    }

    public Guid ReportId { get; }
}
