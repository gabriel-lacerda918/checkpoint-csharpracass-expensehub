#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Linq;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportScopeFilterTests
{
    [TestMethod]
    public void Period_IncludesBothEndsAndExcludesTheDaysAround()
    {
        Expense dayBefore = ReportData.NewExpense("me", ExpenseStatus.Paid, 1m, ReportData.From.AddDays(-1));
        Expense firstDay = ReportData.NewExpense("me", ExpenseStatus.Paid, 2m, ReportData.From);
        Expense lastDay = ReportData.NewExpense("me", ExpenseStatus.Paid, 4m, ReportData.To);
        Expense dayAfter = ReportData.NewExpense("me", ExpenseStatus.Paid, 8m, ReportData.To.AddDays(1));

        Expense[] selected = Select(ReportData.Report("me", ReportScope.Mine), dayBefore, firstDay, lastDay, dayAfter);

        CollectionAssert.AreEquivalent(new[] { firstDay, lastDay }, selected);
    }

    [TestMethod]
    public void Period_UsesTheExpenseDateAndNotTheCreationDate()
    {
        // Both expenses were created on 2026-10-01 (outside the period); only the expense date decides.
        Expense dateInside = ReportData.NewExpense("me", ExpenseStatus.Approved, 10m, new DateOnly(2026, 9, 15));
        Expense dateOutside = ReportData.NewExpense("me", ExpenseStatus.Approved, 20m, new DateOnly(2026, 10, 1));

        Expense[] selected = Select(ReportData.Report("me", ReportScope.Mine), dateInside, dateOutside);

        CollectionAssert.AreEquivalent(new[] { dateInside }, selected);
    }

    [TestMethod]
    public void Mine_NeverIncludesExpensesOfAnotherEmployee()
    {
        Expense mine = ReportData.NewExpense("me", ExpenseStatus.Approved, 10m, new DateOnly(2026, 9, 10));
        Expense theirs = ReportData.NewExpense("other", ExpenseStatus.Approved, 20m, new DateOnly(2026, 9, 10));

        Expense[] selected = Select(ReportData.Report("me", ReportScope.Mine), mine, theirs);

        CollectionAssert.AreEquivalent(new[] { mine }, selected);
    }

    [TestMethod]
    public void Global_IncludesEveryEmployeeInsideThePeriod()
    {
        Expense mine = ReportData.NewExpense("me", ExpenseStatus.Approved, 10m, new DateOnly(2026, 9, 10));
        Expense theirs = ReportData.NewExpense("other", ExpenseStatus.Draft, 20m, new DateOnly(2026, 9, 11));
        Expense outside = ReportData.NewExpense("other", ExpenseStatus.Paid, 30m, new DateOnly(2026, 8, 31));

        Expense[] selected = Select(ReportData.Report("me", ReportScope.Global), mine, theirs, outside);

        CollectionAssert.AreEquivalent(new[] { mine, theirs }, selected);
    }

    [TestMethod]
    public void SinglePeriodDay_SelectsOnlyThatDay()
    {
        DateOnly day = new(2026, 9, 5);
        var report = ExpenseReport.Create("me", ReportScope.Mine, day, day, DateTime.UtcNow);
        Expense inside = ReportData.NewExpense("me", ExpenseStatus.Paid, 1m, day);
        Expense next = ReportData.NewExpense("me", ExpenseStatus.Paid, 2m, day.AddDays(1));

        Expense[] selected = Select(report, inside, next);

        CollectionAssert.AreEquivalent(new[] { inside }, selected);
    }

    private static Expense[] Select(ExpenseReport report, params Expense[] expenses)
    {
        return expenses.AsQueryable().Where(ReportScopeFilter.For(report)).ToArray();
    }
}
