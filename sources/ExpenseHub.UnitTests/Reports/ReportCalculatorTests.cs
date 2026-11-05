#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportCalculatorTests
{
    private static readonly DateTime GeneratedAt = new(2026, 10, 1, 12, 0, 2, DateTimeKind.Utc);

    [TestMethod]
    public void Mine_WithTheSpecificationExample_SumsThreeExpensesAndOneHundredSeventyFive()
    {
        ReportResultResponse result = Summarize(ReportData.Report("me", ReportScope.Mine));

        Assert.AreEqual(3, result.TotalCount);
        Assert.AreEqual(175.00m, result.TotalAmount);
        Assert.AreEqual(2, Item(result, "Approved").Count);
        Assert.AreEqual(150.00m, Item(result, "Approved").Amount);
        Assert.AreEqual(1, Item(result, "Paid").Count);
        Assert.AreEqual(25.00m, Item(result, "Paid").Amount);
    }

    [TestMethod]
    public void Global_WithTheSpecificationExample_SumsFourExpensesAndThreeHundredSeventyFive()
    {
        ReportResultResponse result = Summarize(ReportData.Report("me", ReportScope.Global));

        Assert.AreEqual(4, result.TotalCount);
        Assert.AreEqual(375.00m, result.TotalAmount);
        Assert.AreEqual(3, Item(result, "Approved").Count);
        Assert.AreEqual(350.00m, Item(result, "Approved").Amount);
    }

    [TestMethod]
    public void NoEligibleExpense_ReturnsZeroTotalsAndAllFiveStatusesWithZero()
    {
        ReportResultResponse result = ReportCalculator.Summarize(new List<ExpenseFigure>(), GeneratedAt);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0m, result.TotalAmount);
        Assert.HasCount(5, result.ByStatus);
        Assert.IsTrue(result.ByStatus.All(item => item.Count == 0 && item.Amount == 0m));
    }

    [TestMethod]
    public void Result_AlwaysListsTheFiveStatusesInLifecycleOrder()
    {
        ReportResultResponse result = ReportCalculator.Summarize(new List<ExpenseFigure>(), GeneratedAt);

        string[] statuses = result.ByStatus.Select(item => item.Status).ToArray();
        var expected = new List<string> { "Draft", "Submitted", "Approved", "Rejected", "Paid" };
        CollectionAssert.AreEqual(expected, statuses);
    }

    [TestMethod]
    public void Amounts_UseDecimalArithmeticWithoutFloatingPointError()
    {
        var figures = new List<ExpenseFigure>
        {
            new(ExpenseStatus.Approved, 0.1m),
            new(ExpenseStatus.Approved, 0.2m),
            new(ExpenseStatus.Paid, 0.7m),
        };

        ReportResultResponse result = ReportCalculator.Summarize(figures, GeneratedAt);

        Assert.AreEqual(0.3m, Item(result, "Approved").Amount);
        Assert.AreEqual(1.0m, result.TotalAmount);
    }

    [TestMethod]
    public void Summary_CountsEveryStatusSeparately()
    {
        var figures = new List<ExpenseFigure>
        {
            new(ExpenseStatus.Draft, 1m),
            new(ExpenseStatus.Submitted, 2m),
            new(ExpenseStatus.Rejected, 4m),
            new(ExpenseStatus.Rejected, 8m),
        };

        ReportResultResponse result = ReportCalculator.Summarize(figures, GeneratedAt);

        Assert.AreEqual(1m, Item(result, "Draft").Amount);
        Assert.AreEqual(2m, Item(result, "Submitted").Amount);
        Assert.AreEqual(12m, Item(result, "Rejected").Amount);
        Assert.AreEqual(2, Item(result, "Rejected").Count);
        Assert.AreEqual(0, Item(result, "Paid").Count);
        Assert.AreEqual(4, result.TotalCount);
        Assert.AreEqual(15m, result.TotalAmount);
    }

    [TestMethod]
    public void Result_CarriesTheGenerationInstantGivenByTheWorker()
    {
        ReportResultResponse result = ReportCalculator.Summarize(new List<ExpenseFigure>(), GeneratedAt);

        Assert.AreEqual(GeneratedAt, result.GeneratedAt);
    }

    [TestMethod]
    public void SerializedResult_RoundTripsWithoutLosingDecimalsOrStatuses()
    {
        ReportResultResponse original = Summarize(ReportData.Report("me", ReportScope.Mine));

        ReportResultResponse restored = ReportResultSerializer.Deserialize(ReportResultSerializer.Serialize(original));

        Assert.AreEqual(original.GeneratedAt, restored.GeneratedAt);
        Assert.AreEqual(original.TotalCount, restored.TotalCount);
        Assert.AreEqual(original.TotalAmount, restored.TotalAmount);
        Assert.HasCount(5, restored.ByStatus);
        Assert.AreEqual(150.00m, Item(restored, "Approved").Amount);
    }

    private static ReportResultResponse Summarize(ExpenseReport report)
    {
        IEnumerable<ExpenseFigure> figures = ReportData.SpecificationExample()
            .AsQueryable()
            .Where(ReportScopeFilter.For(report))
            .Select(expense => new ExpenseFigure(expense.Status, expense.Amount));

        return ReportCalculator.Summarize(figures, GeneratedAt);
    }

    private static StatusSummaryResponse Item(ReportResultResponse result, string status)
    {
        return result.ByStatus.Single(item => item.Status == status);
    }
}
