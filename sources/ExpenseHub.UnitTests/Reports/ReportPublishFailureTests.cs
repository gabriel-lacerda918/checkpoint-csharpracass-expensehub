#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Http;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportPublishFailureTests
{
    private readonly ReportFixture _fx = new();

    [TestMethod]
    public async Task WhenTheBrokerDoesNotConfirm_NoSuccessIsReturned_AndTheRegisteredIdIsReported()
    {
        _fx.Publisher.Failure = new TimeoutException("no confirmation");

        ReportPublishException exception = await Assert.ThrowsExactlyAsync<ReportPublishException>(
            () => _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None));

        Assert.IsTrue(_fx.Repository.IsSaved(exception.ReportId));
        Assert.AreNotEqual(Guid.Empty, exception.ReportId);
    }

    [TestMethod]
    public async Task WhenTheBrokerDoesNotConfirm_TheRegisteredRequestIsPreservedAsPending()
    {
        _fx.Publisher.Failure = new TimeoutException("no confirmation");

        ReportPublishException exception = await Assert.ThrowsExactlyAsync<ReportPublishException>(
            () => _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None));

        var reports = await _fx.Web.ListAsync(Users.Employee("emp-1"), CancellationToken.None);
        Assert.HasCount(1, reports);
        Assert.AreEqual(exception.ReportId, reports[0].Id);
        Assert.AreEqual("Pending", reports[0].Status);
    }

    [TestMethod]
    public async Task WhenTheDatabaseFailsBeforeRegistering_NothingIsPublished_AndNoIdIsInvented()
    {
        _fx.Repository.SaveScript.Enqueue(new InvalidOperationException("database unavailable"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None));

        Assert.IsEmpty(_fx.Publisher.Published);
        Assert.IsEmpty(_fx.Repository.SavedReports());
    }

    [TestMethod]
    public void PublishFailure_IsReportedAsServiceUnavailableWithTheReportId()
    {
        Guid id = Guid.NewGuid();

        ProblemDetails? problem = ApiExceptionMapper.Map(new ReportPublishException(id, new TimeoutException("secret broker detail")));

        Assert.IsNotNull(problem);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.AreEqual(id, problem.Extensions["reportId"]);
        Assert.DoesNotContain("secret broker detail", problem.Detail ?? string.Empty);
    }
}
