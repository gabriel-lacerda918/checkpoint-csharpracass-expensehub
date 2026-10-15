using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Immutable audit entry describing one action performed on an expense.
/// </summary>
internal sealed class ExpenseHistory
{
    private ExpenseHistory()
    {
        ActorId = string.Empty;
    }

    public long Id { get; private set; }

    public Guid ExpenseId { get; private set; }

    public ExpenseAction Action { get; private set; }

    public string ActorId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public ExpenseStatus? PreviousStatus { get; private set; }

    public ExpenseStatus NewStatus { get; private set; }

    public string? Reason { get; private set; }

    public string? Changes { get; private set; }

    public static ExpenseHistory Record(
        Guid expenseId,
        ExpenseAction action,
        string actorId,
        DateTime occurredAtUtc,
        ExpenseStatus? previousStatus,
        ExpenseStatus newStatus,
        string? reason,
        string? changes)
    {
        return new ExpenseHistory
        {
            ExpenseId = expenseId,
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Reason = reason,
            Changes = changes,
        };
    }
}
