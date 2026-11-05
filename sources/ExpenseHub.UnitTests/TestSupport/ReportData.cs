using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.TestSupport;

/// <summary>
/// Builds the expenses and requests used by the report tests.
/// </summary>
internal static class ReportData
{
    public static readonly DateOnly From = new(2026, 9, 1);
    public static readonly DateOnly To = new(2026, 9, 30);

    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    public static Expense NewExpense(string ownerId, ExpenseStatus status, decimal amount, DateOnly date)
    {
        Expense expense = Expense.CreateDraft(ownerId, "Expense used by a report test", amount, date, null, Created);

        if (status >= ExpenseStatus.Submitted)
        {
            expense.Submit(Created);
        }

        switch (status)
        {
            case ExpenseStatus.Approved:
            case ExpenseStatus.Paid:
                expense.Approve(Created);
                break;
            case ExpenseStatus.Rejected:
                expense.Reject("Receipt is missing for this expense", Created);
                break;
        }

        if (status == ExpenseStatus.Paid)
        {
            expense.MarkPaid(Created);
        }

        return expense;
    }

    /// <summary>
    /// The example of the specification: three expenses of "me" in the period (150.00 approved, 25.00 paid),
    /// one of another employee (200.00) and one of "me" outside the period (500.00).
    /// </summary>
    public static Expense[] SpecificationExample()
    {
        return new[]
        {
            NewExpense("me", ExpenseStatus.Approved, 100.00m, new DateOnly(2026, 9, 10)),
            NewExpense("me", ExpenseStatus.Approved, 50.00m, new DateOnly(2026, 9, 20)),
            NewExpense("me", ExpenseStatus.Paid, 25.00m, new DateOnly(2026, 9, 30)),
            NewExpense("other", ExpenseStatus.Approved, 200.00m, new DateOnly(2026, 9, 15)),
            NewExpense("me", ExpenseStatus.Paid, 500.00m, new DateOnly(2026, 10, 1)),
        };
    }

    public static ExpenseReport Report(string requesterId, ReportScope scope)
    {
        return ExpenseReport.Create(requesterId, scope, From, To, Created);
    }
}
