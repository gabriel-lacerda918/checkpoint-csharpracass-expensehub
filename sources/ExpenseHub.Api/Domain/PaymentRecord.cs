using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Simulated payment registered by the finance department.
/// </summary>
internal sealed class PaymentRecord
{
    private PaymentRecord()
    {
        PaidByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ExpenseId { get; private set; }

    public string PaidByUserId { get; private set; }

    public DateTime PaidAtUtc { get; private set; }

    public decimal Amount { get; private set; }

    public static PaymentRecord Create(Guid expenseId, string paidByUserId, decimal amount, DateTime paidAtUtc)
    {
        return new PaymentRecord
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            PaidByUserId = paidByUserId,
            Amount = amount,
            PaidAtUtc = paidAtUtc,
        };
    }
}
