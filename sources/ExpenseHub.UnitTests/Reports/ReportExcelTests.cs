#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Application.Reports.Excel;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportExcelTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private readonly ReportFixture _fx = new();
    private readonly CurrentUser _employee = Users.Employee("me");

    [TestMethod]
    public async Task Download_OfACompletedReport_ReturnsAnXlsxFileWithTheContractedNameAndType()
    {
        ReportResponse report = await CompleteAsync();

        ReportFile file = await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None);

        Assert.AreEqual("report-" + report.Id + ".xlsx", file.FileName);
        Assert.AreEqual("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.IsNotEmpty(file.Content);
    }

    [TestMethod]
    public async Task Download_IsARealOfficeOpenXmlPackage_NotRenamedText()
    {
        ReportResponse report = await CompleteAsync();

        ReportFile file = await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None);

        // A real .xlsx is a ZIP archive: it starts with the "PK" signature and holds the workbook parts.
        Assert.AreEqual((byte)'P', file.Content[0]);
        Assert.AreEqual((byte)'K', file.Content[1]);
        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        string[] parts = archive.Entries.Select(entry => entry.FullName).ToArray();
        foreach (string required in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml" })
        {
            Assert.Contains(required, parts);
        }
    }

    [TestMethod]
    public async Task Download_ShowsIdentifierScopePeriodAndGenerationInstant()
    {
        ReportResponse report = await CompleteAsync();

        Dictionary<string, Cell> cells = Read((await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None)).Content);

        Assert.AreEqual("Report ID", cells["A1"].Value);
        Assert.AreEqual(report.Id.ToString(), cells["B1"].Value);
        Assert.AreEqual("Mine", cells["B2"].Value);
        Assert.AreEqual("2026-09-01", cells["B3"].Value);
        Assert.AreEqual("2026-09-30", cells["B4"].Value);
        Assert.AreEqual("2026-10-13T12:00:00Z", cells["B5"].Value);
    }

    [TestMethod]
    public async Task Download_ListsTheFiveStatusesAndATotalMatchingThePersistedResult()
    {
        ReportResponse report = await CompleteAsync();

        Dictionary<string, Cell> cells = Read((await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None)).Content);

        Assert.AreEqual("Status", cells["A7"].Value);
        Assert.AreEqual("Count", cells["B7"].Value);
        Assert.AreEqual("Amount", cells["C7"].Value);
        string[] statuses = { "Draft", "Submitted", "Approved", "Rejected", "Paid" };
        for (int index = 0; index < statuses.Length; index++)
        {
            Assert.AreEqual(statuses[index], cells["A" + (8 + index)].Value);
        }

        Assert.AreEqual("2", cells["B10"].Value);
        Assert.AreEqual("150.00", cells["C10"].Value);
        Assert.AreEqual("1", cells["B12"].Value);
        Assert.AreEqual("25.00", cells["C12"].Value);
        Assert.AreEqual("Total", cells["A13"].Value);
        Assert.AreEqual("3", cells["B13"].Value);
        Assert.AreEqual("175.00", cells["C13"].Value);
    }

    [TestMethod]
    public async Task Download_KeepsCountsAndAmountsAsNumericCells()
    {
        ReportResponse report = await CompleteAsync();

        Dictionary<string, Cell> cells = Read((await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None)).Content);

        foreach (string reference in new[] { "B8", "B10", "B12", "B13", "C8", "C10", "C12", "C13" })
        {
            Assert.IsTrue(cells[reference].IsNumeric, reference);
        }

        Assert.IsFalse(cells["A8"].IsNumeric);
    }

    [TestMethod]
    public async Task Download_OfAnEmptyReport_KeepsAllFiveStatusesWithNumericZeros()
    {
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);

        Dictionary<string, Cell> cells = Read((await _fx.Web.GetExcelAsync(_employee, created.Id, CancellationToken.None)).Content);

        for (int row = 8; row <= 12; row++)
        {
            Assert.AreEqual("0", cells["B" + row].Value);
            Assert.AreEqual("0", cells["C" + row].Value);
            Assert.IsTrue(cells["B" + row].IsNumeric);
        }

        Assert.AreEqual("0", cells["B13"].Value);
        Assert.AreEqual("0", cells["C13"].Value);
    }

    [TestMethod]
    public async Task Download_AfterAnExpenseChanges_StillHoldsTheSavedValuesAndDoesNotQueryAgain()
    {
        ReportResponse report = await CompleteAsync();
        Dictionary<string, Cell> before = Read((await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None)).Content);
        int queries = _fx.DataSource.QueryCount;

        _fx.DataSource.Expenses.Add(ReportData.NewExpense("me", ExpenseStatus.Paid, 5000m, new DateOnly(2026, 9, 12)));
        Dictionary<string, Cell> after = Read((await _fx.Web.GetExcelAsync(_employee, report.Id, CancellationToken.None)).Content);

        Assert.AreEqual("175.00", after["C13"].Value);
        Assert.AreEqual(before["C13"].Value, after["C13"].Value);
        Assert.AreEqual(queries, _fx.DataSource.QueryCount);
    }

    [TestMethod]
    public async Task Download_OfAReportThatIsNotCompleted_IsAConflictForItsOwner()
    {
        ReportResponse pending = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        await Assert.ThrowsExactlyAsync<StateConflictException>(
            () => _fx.Web.GetExcelAsync(_employee, pending.Id, CancellationToken.None));

        ReportResponse processing = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.DataSource.Failure = new InvalidOperationException("stopped");
        await _fx.Worker.HandleAsync(processing.Id, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<StateConflictException>(
            () => _fx.Web.GetExcelAsync(_employee, processing.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task Download_OfAFailedReport_IsAConflictAndExposesNoFile()
    {
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        _fx.DataSource.Failure = new InvalidOperationException("always fails");
        for (int attempt = 0; attempt <= ExpenseReport.MaxAttempts; attempt++)
        {
            await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);
        }

        await Assert.ThrowsExactlyAsync<StateConflictException>(
            () => _fx.Web.GetExcelAsync(_employee, created.Id, CancellationToken.None));
    }

    [TestMethod]
    public void Workbook_EscapesSpecialCharactersInText()
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            new[] { XlsxCell.FromText("<a> & \"b\""), XlsxCell.FromInteger(7), XlsxCell.FromMoney(1.50m) },
        };

        Dictionary<string, Cell> cells = Read(XlsxWorkbookBuilder.Build("Sheet", rows));

        Assert.AreEqual("<a> & \"b\"", cells["A1"].Value);
        Assert.AreEqual("7", cells["B1"].Value);
        Assert.AreEqual("1.50", cells["C1"].Value);
    }

    [TestMethod]
    public void ColumnNames_FollowTheSpreadsheetConvention()
    {
        Assert.AreEqual("A", XlsxWorkbookBuilder.ColumnName(0));
        Assert.AreEqual("Z", XlsxWorkbookBuilder.ColumnName(25));
        Assert.AreEqual("AA", XlsxWorkbookBuilder.ColumnName(26));
        Assert.AreEqual("AB", XlsxWorkbookBuilder.ColumnName(27));
        Assert.AreEqual("ZZ", XlsxWorkbookBuilder.ColumnName(701));
        Assert.AreEqual("AAA", XlsxWorkbookBuilder.ColumnName(702));
    }

    private async Task<ReportResponse> CompleteAsync()
    {
        _fx.DataSource.Expenses.AddRange(ReportData.SpecificationExample());
        ReportResponse created = await _fx.Web.RequestAsync(_employee, ReportFixture.Input("Mine"), CancellationToken.None);
        await _fx.Worker.HandleAsync(created.Id, CancellationToken.None);

        return created;
    }

    private static Dictionary<string, Cell> Read(byte[] workbook)
    {
        using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        using Stream stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XDocument sheet = XDocument.Load(stream);

        var cells = new Dictionary<string, Cell>();
        foreach (XElement element in sheet.Descendants(Main + "c"))
        {
            string reference = element.Attribute("r")!.Value;
            bool isText = element.Attribute("t")?.Value == "inlineStr";
            string value = isText ? element.Element(Main + "is")!.Element(Main + "t")!.Value : element.Element(Main + "v")!.Value;
            cells[reference] = new Cell(value, !isText);
        }

        return cells;
    }

    private sealed record Cell(string Value, bool IsNumeric);
}
