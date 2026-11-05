using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Application.Reports.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseHub.UnitTests.TestSupport;

/// <summary>
/// A fresh set of fakes and services for each test: the web side and the worker side share one repository,
/// just as the two processes share one database.
/// </summary>
internal sealed class ReportFixture
{
    public ReportFixture()
    {
        Repository = new InMemoryReportRepository();
        DataSource = new FakeReportDataSource();
        Publisher = new FakeReportPublisher();
        Clock = new TestClock();
        Web = new ReportService(Repository, Publisher, Clock, NullLogger<ReportService>.Instance);
        Worker = NewWorker();
    }

    public InMemoryReportRepository Repository { get; }

    public FakeReportDataSource DataSource { get; }

    public FakeReportPublisher Publisher { get; }

    public TestClock Clock { get; }

    public ReportService Web { get; }

    public ReportProcessingService Worker { get; }

    /// <summary>
    /// A new worker instance over the same storage, which is what a restarted worker is.
    /// </summary>
    public ReportProcessingService NewWorker()
    {
        return new ReportProcessingService(Repository, DataSource, Clock, NullLogger<ReportProcessingService>.Instance);
    }

    public static ReportInput Input(string scope)
    {
        return new ReportInput(ReportData.From, ReportData.To, scope);
    }
}
