namespace ExpenseHub.Api.Domain;

/// <summary>
/// Actions recorded in the expense history.
/// </summary>
internal enum ExpenseAction
{
    Created = 0,
    Updated = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Paid = 5,
}
