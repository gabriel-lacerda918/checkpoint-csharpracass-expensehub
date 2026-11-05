#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Text;
using ExpenseHub.Api.Application.Reports.Processing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportMessageTests
{
    [TestMethod]
    public void Serialize_ProducesAMessageWithOnlyTheReportId()
    {
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        string json = Encoding.UTF8.GetString(ReportMessage.Serialize(id));

        Assert.AreEqual("{\"reportId\":\"11111111-2222-3333-4444-555555555555\"}", json);
    }

    [TestMethod]
    public void TryParse_ReadsTheMessageThatSerializeProduces()
    {
        Guid id = Guid.NewGuid();

        Guid? parsed = ReportMessage.TryParse(ReportMessage.Serialize(id));

        Assert.AreEqual(id, parsed);
    }

    [TestMethod]
    public void TryParse_IgnoresAnyOtherFieldInTheMessage()
    {
        Guid id = Guid.NewGuid();
        byte[] payload = Encoding.UTF8.GetBytes("{\"reportId\":\"" + id + "\",\"requesterId\":\"someone-else\",\"scope\":\"Global\"}");

        Guid? parsed = ReportMessage.TryParse(payload);

        Assert.AreEqual(id, parsed);
    }

    [TestMethod]
    public void TryParse_WithAnInvalidPayload_ReturnsNull()
    {
        foreach (string payload in new[] { string.Empty, "not json", "{}", "{\"reportId\":\"abc\"}", "{\"reportId\":\"00000000-0000-0000-0000-000000000000\"}", "[]", "null" })
        {
            Assert.IsNull(ReportMessage.TryParse(Encoding.UTF8.GetBytes(payload)));
        }
    }
}
