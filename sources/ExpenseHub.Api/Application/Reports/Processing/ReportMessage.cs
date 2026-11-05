using System;
using System.Text.Json;

namespace ExpenseHub.Api.Application.Reports.Processing;

/// <summary>
/// The queue message carries only the report id. Period, scope and requester are read from the stored request,
/// so nothing inside a message can change who the report belongs to.
/// </summary>
internal static class ReportMessage
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize(Guid reportId)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new ReportMessageBody(reportId), Options);
    }

    /// <summary>
    /// Returns the report id, or null when the payload is not a valid message.
    /// </summary>
    public static Guid? TryParse(ReadOnlySpan<byte> payload)
    {
        try
        {
            ReportMessageBody? body = JsonSerializer.Deserialize<ReportMessageBody>(payload, Options);

            return body is null || body.ReportId == Guid.Empty ? null : body.ReportId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ReportMessageBody(Guid ReportId);
}
