#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportPersistedResultTests
{
    private readonly ReportFixture _fx = new();
    private readonly CurrentUser _employee = Users.Employee("me");

    [TestMethod]
    public async Task Pending_ExposesOnlyTheCreationInstant()
    {
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);

        ReportResponse detail = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);

        Assert.AreEqual("Pending", detail.Status);
        Assert.AreEqual(_fx.Clock.UtcNow, detail.CreatedAt);
        Assert.IsNull(detail.StartedAt);
        Assert.IsNull(detail.CompletedAt);
        Assert.IsNull(detail.Result);
        Assert.IsNull(detail.Error);
    }

    [TestMethod]
    public async Task Processing_ExposesTheStartInstantButNoResult()
    {
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.DataSource.Failure = new InvalidOperationException("worker is busy or stopped");
        await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);

        ReportResponse detail = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);

        Assert.AreEqual("Processing", detail.Status);
        Assert.IsNotNull(detail.StartedAt);
        Assert.IsNull(detail.CompletedAt);
        Assert.IsNull(detail.Result);
    }

    [TestMethod]
    public async Task Completed_ExposesTheResultAndTheCompletionInstant_WithoutError()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.Clock.Advance(TimeSpan.FromSeconds(2));
        await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);

        ReportResponse detail = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);

        Assert.AreEqual("Completed", detail.Status);
        Assert.AreEqual(_fx.Clock.UtcNow, detail.CompletedAt);
        Assert.AreEqual(_fx.Clock.UtcNow, detail.Result!.GeneratedAt);
        Assert.IsNull(detail.Error);
        Assert.AreEqual(DateTimeKind.Utc, detail.CompletedAt!.Value.Kind);
    }

    [TestMethod]
    public async Task Failed_ExposesAStableCodeAndASanitizedMessage_WithoutResult()
    {
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.DataSource.Failure = new InvalidOperationException("Server=db;User=admin connection details");
        for (int attempt = 0; attempt <= ExpenseReport.MaxAttempts; attempt++)
        {
            await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);
        }

        ReportResponse detail = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);

        Assert.AreEqual("Failed", detail.Status);
        Assert.IsNull(detail.Result);
        Assert.IsNotNull(detail.CompletedAt);
        Assert.AreEqual("MAX_ATTEMPTS_EXCEEDED", detail.Error!.Code);
        Assert.DoesNotContain("Server=", detail.Error.Message);
    }

    [TestMethod]
    public async Task Detail_AfterAnExpenseIsChanged_StillReturnsTheSavedResultWithoutRecalculating()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);
        ReportResponse before = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);
        int queriesBefore = _fx.DataSource.QueryCount;

        _fx.DataSource.Expenses.Add(ReportData.NewExpense("me", ExpenseStatus.Paid, 1000m, new DateOnly(2026, 9, 12)));
        _fx.Clock.Advance(TimeSpan.FromDays(1));
        ReportResponse after = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);

        Assert.AreEqual(before.Result!.TotalAmount, after.Result!.TotalAmount);
        Assert.AreEqual(175.00m, after.Result.TotalAmount);
        Assert.AreEqual(before.Result.GeneratedAt, after.Result.GeneratedAt);
        Assert.AreEqual(queriesBefore, _fx.DataSource.QueryCount);
    }

    [TestMethod]
    public async Task Result_ReflectsTheExpensesAsReadWhileProcessing_NotWhenTheRequestWasCreated()
    {
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.DataSource.Expenses.Add(ReportData.NewExpense("me", ExpenseStatus.Paid, 40m, new DateOnly(2026, 9, 12)));

        await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);

        ReportResponse detail = await _fx.Web.GetAsync(_employee, created.Id, CancellationToken.None);
        Assert.AreEqual(40m, detail.Result!.TotalAmount);
    }

    [TestMethod]
    public async Task List_ReturnsTheEnvelopesNewestFirst()
    {
        ReportResponse older = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.Clock.Advance(TimeSpan.FromMinutes(1));
        ReportResponse newer = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);

        IReadOnlyList<ReportResponse> list = await _fx.Web.ListAsync(_employee, CancellationToken.None);

        Assert.HasCount(2, list);
        Assert.AreEqual(newer.Id, list[0].Id);
        Assert.AreEqual(older.Id, list[1].Id);
    }
}
