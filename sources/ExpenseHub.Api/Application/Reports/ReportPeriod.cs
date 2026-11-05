using System;
using System.Collections.Generic;
using ExpenseHub.Api.Application.Exceptions;

namespace ExpenseHub.Api.Application.Reports;

internal static class ReportPeriod
{
    public static (DateOnly From, DateOnly To) Validate(DateOnly? fromDate, DateOnly? toDate)
    {
        var errors = new Dictionary<string, string[]>();

        if (fromDate is null)
        {
            errors["FromDate"] = new[] { "FromDate is required." };
        }

        if (toDate is null)
        {
            errors["ToDate"] = new[] { "ToDate is required." };
        }

        if (fromDate is not null && toDate is not null && fromDate > toDate)
        {
            errors["FromDate"] = new[] { "FromDate must be less than or equal to ToDate." };
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return (fromDate!.Value, toDate!.Value);
    }
}
