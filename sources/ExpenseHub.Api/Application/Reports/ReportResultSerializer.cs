using System;
using System.Text.Json;
using ExpenseHub.Api.Contracts;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// Converts the result to the text stored with the request and back, preserving every decimal exactly.
/// </summary>
internal static class ReportResultSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(ReportResultResponse result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static ReportResultResponse Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ReportResultResponse>(json, Options)
            ?? throw new InvalidOperationException("The stored report result is empty.");
    }
}
