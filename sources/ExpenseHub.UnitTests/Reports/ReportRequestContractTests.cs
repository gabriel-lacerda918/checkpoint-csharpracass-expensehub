#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Dtos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportRequestContractTests
{
    [TestMethod]
    public void CreateReportRequest_HasNoOwnerOrIdentityField()
    {
        string[] properties = typeof(CreateReportRequest).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var expected = new List<string> { "FromDate", "Scope", "ToDate" };

        CollectionAssert.AreEqual(expected, properties);
    }

    [TestMethod]
    public void CreateReportRequest_WithAllFields_PassesValidation()
    {
        var request = new CreateReportRequest { FromDate = new DateOnly(2026, 9, 1), ToDate = new DateOnly(2026, 9, 30), Scope = "Mine" };

        Assert.IsEmpty(Validate(request));
    }

    [TestMethod]
    public void CreateReportRequest_WithoutFields_ReportsEachRequiredField()
    {
        Assert.HasCount(3, Validate(new CreateReportRequest()));
    }

    [TestMethod]
    public void CreateReportRequest_WithOnlyOneDate_ReportsTheMissingOne()
    {
        var request = new CreateReportRequest { FromDate = new DateOnly(2026, 9, 1), Scope = "Mine" };

        List<ValidationResult> results = Validate(request);

        Assert.HasCount(1, results);
        Assert.Contains("ToDate", results[0].MemberNames);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        return results;
    }
}
