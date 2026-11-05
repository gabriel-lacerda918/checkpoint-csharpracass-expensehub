namespace ExpenseHub.Api.Contracts;

/// <summary>
/// Quantity and amount of the expenses in one status.
/// </summary>
/// <param name="Status">Expense status (Draft, Submitted, Approved, Rejected or Paid).</param>
/// <param name="Count">Number of expenses in the status.</param>
/// <param name="Amount">Sum of their amounts.</param>
public sealed record StatusSummaryResponse(string Status, int Count, decimal Amount);
