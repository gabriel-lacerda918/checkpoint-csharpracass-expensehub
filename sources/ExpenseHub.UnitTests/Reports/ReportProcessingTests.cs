#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Application.Reports.Processing;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportProcessingTests
{
    private readonly ReportFixture _fx = new();

    [TestMethod]
    public async Task PendingReport_IsCompleted_WithTheResultSavedTogetherWithTheStatus()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ExpenseReport report = Seed("me", ReportScope.Mine);

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Completed, outcome);
        Assert.AreEqual(ReportStatus.Completed, _fx.Repository.SavedStatus(report.Id));
        ReportResultResponse saved = ReportResultSerializer.Deserialize(_fx.Repository.SavedResult(report.Id)!);
        Assert.AreEqual(175.00m, saved.TotalAmount);
        Assert.AreEqual(_fx.Clock.UtcNow, saved.GeneratedAt);
        Assert.IsNotNull(report.StartedAtUtc);
        Assert.AreEqual(_fx.Clock.UtcNow, report.CompletedAtUtc);
        Assert.IsNull(report.ErrorCode);
    }

    [TestMethod]
    public async Task OnlyAfterTheResultIsSaved_MayTheMessageBeAcknowledged()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.IsTrue(ReportProcessingService.ShouldAcknowledge(outcome));
        Assert.IsNotNull(_fx.Repository.SavedResult(report.Id));
    }

    [TestMethod]
    public async Task ReportWithoutExpenses_IsCompletedWithZeros()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);

        await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        ReportResultResponse saved = ReportResultSerializer.Deserialize(_fx.Repository.SavedResult(report.Id)!);
        Assert.AreEqual(0, saved.TotalCount);
        Assert.AreEqual(0m, saved.TotalAmount);
        Assert.HasCount(5, saved.ByStatus);
    }

    [TestMethod]
    public async Task DuplicateDelivery_OfACompletedReport_HasNoNewEffect()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ExpenseReport report = Seed("me", ReportScope.Mine);
        await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        string resultBefore = _fx.Repository.SavedResult(report.Id)!;
        int savesBefore = _fx.Repository.SaveCount;
        int queriesBefore = _fx.DataSource.QueryCount;
        _fx.Clock.Advance(TimeSpan.FromHours(1));

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Duplicate, outcome);
        Assert.IsTrue(ReportProcessingService.ShouldAcknowledge(outcome));
        Assert.AreEqual(resultBefore, _fx.Repository.SavedResult(report.Id));
        Assert.AreEqual(savesBefore, _fx.Repository.SaveCount);
        Assert.AreEqual(queriesBefore, _fx.DataSource.QueryCount);
    }

    [TestMethod]
    public async Task DuplicateDelivery_AfterTheWorkerRestarts_IsStillRecognizedFromTheDatabase()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ExpenseReport report = Seed("me", ReportScope.Mine);
        await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        string resultBefore = _fx.Repository.SavedResult(report.Id)!;
        _fx.DataSource.Expenses.Add(ReportData.NewExpense("me", ExpenseStatus.Paid, 999m, new DateOnly(2026, 9, 12)));

        ReportProcessingService restarted = _fx.NewWorker();
        ReportProcessingOutcome outcome = await restarted.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Duplicate, outcome);
        Assert.AreEqual(resultBefore, _fx.Repository.SavedResult(report.Id));
    }

    [TestMethod]
    public async Task DuplicateDelivery_OfAFailedReport_DoesNotReopenIt()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        report.Fail("SOME_CODE", "It failed.", _fx.Clock.UtcNow);
        _fx.Repository.Seed(report);

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Duplicate, outcome);
        Assert.AreEqual(ReportStatus.Failed, _fx.Repository.SavedStatus(report.Id));
        Assert.AreEqual(0, _fx.DataSource.QueryCount);
    }

    [TestMethod]
    public async Task MessageForAReportThatDoesNotExist_IsDiscarded()
    {
        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Discarded, outcome);
        Assert.IsTrue(ReportProcessingService.ShouldAcknowledge(outcome));
        Assert.AreEqual(0, _fx.DataSource.QueryCount);
    }

    [TestMethod]
    public async Task WhenTheCalculationCannotRun_TheMessageIsNotAcknowledged_AndTheReportStaysProcessing()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.DataSource.Failure = new InvalidOperationException("database is down");

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Retry, outcome);
        Assert.IsFalse(ReportProcessingService.ShouldAcknowledge(outcome));
        Assert.AreEqual(ReportStatus.Processing, _fx.Repository.SavedStatus(report.Id));
        Assert.IsNull(_fx.Repository.SavedResult(report.Id));
    }

    [TestMethod]
    public async Task ReportLeftInProcessing_IsResumedByTheNextDelivery_AndKeepsItsFirstStartInstant()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.DataSource.Failure = new InvalidOperationException("worker stopped");
        await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        DateTime firstStart = _fx.Repository.SavedStartedAt(report.Id)!.Value;
        _fx.DataSource.Failure = null;
        _fx.Clock.Advance(TimeSpan.FromMinutes(10));

        ReportProcessingOutcome outcome = await _fx.NewWorker().HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Completed, outcome);
        Assert.AreEqual(ReportStatus.Completed, _fx.Repository.SavedStatus(report.Id));
        Assert.AreEqual(firstStart, _fx.Repository.SavedStartedAt(report.Id));
        Assert.AreEqual(2, _fx.Repository.SavedAttempts(report.Id));
    }

    [TestMethod]
    public async Task WhenTheResultCannotBeSaved_TheMessageIsNotAcknowledged_AndNothingIsReportedAsDone()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.Repository.SaveScript.Enqueue(null);
        _fx.Repository.SaveScript.Enqueue(new InvalidOperationException("disk full"));

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Retry, outcome);
        Assert.AreEqual(ReportStatus.Processing, _fx.Repository.SavedStatus(report.Id));
        Assert.IsNull(_fx.Repository.SavedResult(report.Id));

        ReportProcessingOutcome redelivery = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Completed, redelivery);
        Assert.IsNotNull(_fx.Repository.SavedResult(report.Id));
    }

    [TestMethod]
    public async Task WhenTheDatabaseIsDownBeforeProcessingStarts_TheReportStaysPending_AndTheMessageIsRetried()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.Repository.SaveScript.Enqueue(new InvalidOperationException("database unavailable"));

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Retry, outcome);
        Assert.AreEqual(ReportStatus.Pending, _fx.Repository.SavedStatus(report.Id));
        Assert.AreEqual(0, _fx.DataSource.QueryCount);
    }

    [TestMethod]
    public async Task ConcurrentChange_IsNotAcknowledged_SoTheMessageComesBack()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.Repository.SaveScript.Enqueue(new StateConflictException("changed by another worker"));

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Retry, outcome);
    }

    [TestMethod]
    public async Task AfterTooManyFailedAttempts_TheReportIsRecordedAsFailed_AndTheMessageIsReleased()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.DataSource.Failure = new InvalidOperationException("always fails");

        for (int attempt = 1; attempt <= ExpenseReport.MaxAttempts; attempt++)
        {
            ReportProcessingOutcome retry = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
            Assert.AreEqual(ReportProcessingOutcome.Retry, retry);
        }

        ReportProcessingOutcome final = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Failed, final);
        Assert.IsTrue(ReportProcessingService.ShouldAcknowledge(final));
        Assert.AreEqual(ReportStatus.Failed, _fx.Repository.SavedStatus(report.Id));
        Assert.AreEqual("MAX_ATTEMPTS_EXCEEDED", report.ErrorCode);
        Assert.IsNotNull(report.CompletedAtUtc);
        Assert.IsNull(_fx.Repository.SavedResult(report.Id));

        ReportProcessingOutcome afterwards = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        Assert.AreEqual(ReportProcessingOutcome.Duplicate, afterwards);
    }

    [TestMethod]
    public async Task WhenTheFailureCannotBeRecorded_TheMessageIsNotAcknowledged()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.DataSource.Failure = new InvalidOperationException("always fails");
        for (int attempt = 1; attempt <= ExpenseReport.MaxAttempts; attempt++)
        {
            await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);
        }

        _fx.Repository.SaveScript.Enqueue(new InvalidOperationException("database unavailable"));

        ReportProcessingOutcome outcome = await _fx.Worker.HandleAsync(report.Id, CancellationToken.None);

        Assert.AreEqual(ReportProcessingOutcome.Retry, outcome);
        Assert.AreEqual(ReportStatus.Processing, _fx.Repository.SavedStatus(report.Id));
    }

    [TestMethod]
    public async Task Cancellation_IsNotTurnedIntoARetry()
    {
        ExpenseReport report = Seed("me", ReportScope.Mine);
        _fx.DataSource.Failure = new OperationCanceledException();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => _fx.Worker.HandleAsync(report.Id, CancellationToken.None));
    }

    [TestMethod]
    public void OnlyRetryLeavesTheMessageInTheQueue()
    {
        foreach (ReportProcessingOutcome outcome in Enum.GetValues<ReportProcessingOutcome>())
        {
            bool acknowledge = ReportProcessingService.ShouldAcknowledge(outcome);

            Assert.AreEqual(outcome != ReportProcessingOutcome.Retry, acknowledge);
        }
    }

    private ExpenseReport Seed(string requesterId, ReportScope scope)
    {
        ExpenseReport report = ReportData.Report(requesterId, scope);
        _fx.Repository.Seed(report);

        return report;
    }
}
