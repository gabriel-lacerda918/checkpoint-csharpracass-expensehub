namespace ExpenseHub.Api.Domain;

/// <summary>
/// Lifecycle states of an expense. <see cref="Rejected"/> and <see cref="Paid"/> are final.
/// </summary>
internal enum ExpenseStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Paid = 4,
}
