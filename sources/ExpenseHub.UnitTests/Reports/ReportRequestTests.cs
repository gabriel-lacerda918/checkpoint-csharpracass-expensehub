#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportRequestTests
{
    private readonly ReportFixture _fx = new();

    [TestMethod]
    public async Task Request_Mine_RegistersAPendingReportWithoutResult()
    {
        ReportResponse response = await _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None);

        Assert.AreEqual("Pending", response.Status);
        Assert.AreEqual("Mine", response.Scope);
        Assert.AreEqual(ReportData.From, response.FromDate);
        Assert.AreEqual(ReportData.To, response.ToDate);
        Assert.AreEqual(_fx.Clock.UtcNow, response.CreatedAt);
        Assert.IsNull(response.StartedAt);
        Assert.IsNull(response.CompletedAt);
        Assert.IsNull(response.Result);
        Assert.IsNull(response.Error);
        Assert.AreNotEqual(Guid.Empty, response.Id);
    }

    [TestMethod]
    public async Task Request_IsStoredWithTheRequesterTakenFromTheToken()
    {
        ReportResponse response = await _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None);

        ExpenseReport stored = (await _fx.Repository.FindAsync(response.Id, CancellationToken.None))!;
        Assert.AreEqual("emp-1", stored.RequesterId);
        Assert.AreEqual(ReportScope.Mine, stored.Scope);
        Assert.AreEqual(ReportStatus.Pending, _fx.Repository.SavedStatus(response.Id));
    }

    [TestMethod]
    public async Task Request_IsStoredBeforeItIsPublished()
    {
        bool storedWhenPublished = false;
        _fx.Publisher.OnPublish = id => storedWhenPublished = _fx.Repository.IsSaved(id);

        ReportResponse response = await _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None);

        Assert.IsTrue(storedWhenPublished);
        CollectionAssert.AreEqual(new List<Guid> { response.Id }, _fx.Publisher.Published);
    }

    [TestMethod]
    public async Task Request_NeverCalculatesAnything()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());

        ReportResponse response = await _fx.Web.RequestAsync(Users.Employee("me"), ReportFixture.Input("Mine"), CancellationToken.None);

        Assert.AreEqual(0, _fx.DataSource.QueryCount);
        Assert.IsNull(response.Result);
    }

    [TestMethod]
    public async Task EachValidRequest_CreatesADifferentReportAndMessage()
    {
        CurrentUser employee = Users.Employee("emp-1");

        ReportResponse first = await _fx.Web.RequestAsync(employee, ReportFixture.Input("Mine"), CancellationToken.None);
        ReportResponse second = await _fx.Web.RequestAsync(employee, ReportFixture.Input("Mine"), CancellationToken.None);

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.HasCount(2, _fx.Publisher.Published);
    }

    [TestMethod]
    public async Task Request_WithFromDateAfterToDate_IsRejectedWithoutStoringOrPublishing()
    {
        var inverted = new ReportInput(ReportData.To, ReportData.From, "Mine");

        ValidationFailedException exception = await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _fx.Web.RequestAsync(Users.Employee("emp-1"), inverted, CancellationToken.None));

        Assert.IsTrue(exception.Errors.ContainsKey("FromDate"));
        Assert.IsEmpty(_fx.Repository.SavedReports());
        Assert.IsEmpty(_fx.Publisher.Published);
    }

    [TestMethod]
    public async Task Request_WithMissingDates_IsRejectedWithoutStoringOrPublishing()
    {
        var missing = new ReportInput(null, null, "Mine");

        ValidationFailedException exception = await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _fx.Web.RequestAsync(Users.Employee("emp-1"), missing, CancellationToken.None));

        Assert.IsTrue(exception.Errors.ContainsKey("FromDate"));
        Assert.IsTrue(exception.Errors.ContainsKey("ToDate"));
        Assert.IsEmpty(_fx.Repository.SavedReports());
        Assert.IsEmpty(_fx.Publisher.Published);
    }

    [TestMethod]
    public async Task Request_WithTheSameFromAndToDate_IsAValidSingleDayPeriod()
    {
        var singleDay = new ReportInput(ReportData.From, ReportData.From, "Mine");

        ReportResponse response = await _fx.Web.RequestAsync(Users.Employee("emp-1"), singleDay, CancellationToken.None);

        Assert.AreEqual(response.FromDate, response.ToDate);
    }

    [TestMethod]
    public async Task Request_WithUnknownMissingOrDifferentlyCasedScope_IsRejected()
    {
        foreach (string? scope in new[] { null, string.Empty, "Everyone", "mine", "GLOBAL" })
        {
            ValidationFailedException exception = await Assert.ThrowsExactlyAsync<ValidationFailedException>(
                () => _fx.Web.RequestAsync(Users.Employee("emp-1"), new ReportInput(ReportData.From, ReportData.To, scope), CancellationToken.None));

            Assert.IsTrue(exception.Errors.ContainsKey("Scope"));
        }

        Assert.IsEmpty(_fx.Repository.SavedReports());
        Assert.IsEmpty(_fx.Publisher.Published);
    }
}
