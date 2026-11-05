#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Contracts;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportScopeProcessingTests
{
    private readonly ReportFixture _fx = new();

    [TestMethod]
    public async Task Excel_OfAnotherUsersCompletedReport_IsNotFound_AndExposesNoData()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ReportResponse theirs = await _fx.Web.RequestAsync(Users.Employee("me"), ReportFixture.Input("Mine"), CancellationToken.None);
        await _fx.Worker.HandleAsync(theirs.Id, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _fx.Web.GetExcelAsync(Users.Auditor("aud-1"), theirs.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _fx.Web.GetExcelAsync(Users.Employee("emp-2"), theirs.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _fx.Web.GetExcelAsync(Users.Employee("emp-2"), Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task Mine_ProcessedByTheWorker_NeverSumsAnotherEmployeesExpenses()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ReportResponse report = await _fx.Web.RequestAsync(Users.Employee("me"), ReportFixture.Input("Mine"), CancellationToken.None);

        await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        ReportResponse completed = await _fx.Web.GetAsync(Users.Employee("me"), report.Id, CancellationToken.None);

        Assert.AreEqual(175.00m, completed.Result!.TotalAmount);
        Assert.AreEqual(3, completed.Result.TotalCount);
    }

    [TestMethod]
    public async Task Global_ProcessedByTheWorker_UsesTheScopeStoredForTheAuditor()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ReportResponse report = await _fx.Web.RequestAsync(Users.Auditor("aud-1"), ReportFixture.Input("Global"), CancellationToken.None);

        await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        ReportResponse completed = await _fx.Web.GetAsync(Users.Auditor("aud-1"), report.Id, CancellationToken.None);

        Assert.AreEqual(375.00m, completed.Result!.TotalAmount);
        Assert.AreEqual(4, completed.Result.TotalCount);
    }
}
