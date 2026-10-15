using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// A reimbursement request. All state changes go through the methods of this class,
/// which is the single source of truth for the transition rules.
/// </summary>
internal sealed class Expense
{
    private Expense()
    {
        OwnerId = string.Empty;
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly ExpenseDate { get; private set; }

    public int? CategoryId { get; private set; }

    public ExpenseStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, incremented on every change.
    /// </summary>
    public int Version { get; private set; }

    public static Expense CreateDraft(
        string ownerId,
        string description,
        decimal amount,
        DateOnly expenseDate,
        int? categoryId,
        DateTime nowUtc)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Description = description,
            Amount = amount,
            ExpenseDate = expenseDate,
            CategoryId = categoryId,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// Applies the new values to a draft and returns a description of each field that changed.
    /// </summary>
    public IReadOnlyList<string> UpdateDraft(
        string description,
        decimal amount,
        DateOnly expenseDate,
        int? categoryId,
        DateTime nowUtc)
    {
        EnsureStatus(ExpenseStatus.Draft, "edit");

        var changes = new List<string>();

        if (!string.Equals(Description, description, StringComparison.Ordinal))
        {
            changes.Add(FormattableString.Invariant($"Description: '{Description}' -> '{description}'"));
            Description = description;
        }

        if (Amount != amount)
        {
            changes.Add(FormattableString.Invariant($"Amount: {Amount:0.00} -> {amount:0.00}"));
            Amount = amount;
        }

        if (ExpenseDate != expenseDate)
        {
            changes.Add(FormattableString.Invariant($"ExpenseDate: {ExpenseDate:yyyy-MM-dd} -> {expenseDate:yyyy-MM-dd}"));
            ExpenseDate = expenseDate;
        }

        if (CategoryId != categoryId)
        {
            changes.Add(FormattableString.Invariant($"CategoryId: {Describe(CategoryId)} -> {Describe(categoryId)}"));
            CategoryId = categoryId;
        }

        if (changes.Count > 0)
        {
            Touch(nowUtc);
        }

        return changes;
    }

    public void Submit(DateTime nowUtc)
    {
        Transition(ExpenseStatus.Draft, ExpenseStatus.Submitted, "submit", nowUtc);
    }

    public void Approve(DateTime nowUtc)
    {
        Transition(ExpenseStatus.Submitted, ExpenseStatus.Approved, "approve", nowUtc);
    }

    public void Reject(string reason, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Transition(ExpenseStatus.Submitted, ExpenseStatus.Rejected, "reject", nowUtc);
        RejectionReason = reason;
    }

    public void MarkPaid(DateTime nowUtc)
    {
        Transition(ExpenseStatus.Approved, ExpenseStatus.Paid, "pay", nowUtc);
    }

    private static string Describe(int? categoryId)
    {
        return categoryId is null ? "none" : FormattableString.Invariant($"{categoryId.Value}");
    }

    private void Transition(ExpenseStatus expected, ExpenseStatus next, string operation, DateTime nowUtc)
    {
        EnsureStatus(expected, operation);
        Status = next;
        Touch(nowUtc);
    }

    private void EnsureStatus(ExpenseStatus expected, string operation)
    {
        if (Status != expected)
        {
            throw new StateConflictException(
                FormattableString.Invariant($"Cannot {operation} an expense in status {Status}; expected status {expected}."));
        }
    }

    private void Touch(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}
