using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>
/// Data accepted to request a report. There is no owner field: the requester comes from the authenticated token.
/// </summary>
public sealed class CreateReportRequest
{
    /// <summary>Gets the first day of the period, inclusive (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly? FromDate { get; init; }

    /// <summary>Gets the last day of the period, inclusive (yyyy-MM-dd).</summary>
    [Required]
    public DateOnly? ToDate { get; init; }

    /// <summary>Gets the scope: Mine (own expenses, Employee) or Global (all expenses, Auditor).</summary>
    [Required]
    [StringLength(10)]
    public string? Scope { get; init; }
}
