#pragma warning disable CS1591 // Test names are descriptive; XML docs add no value here.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

[TestClass]
public sealed class ReportAuthorizationTests
{
    private readonly ReportFixture _fx = new();

    [TestMethod]
    public async Task Employee_CanRequestMine()
    {
        ReportResponse response = await _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None);

        Assert.AreEqual("Mine", response.Scope);
    }

    [TestMethod]
    public async Task Employee_CannotRequestGlobal_AndNothingIsStoredOrPublished()
    {
        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Global"), CancellationToken.None));

        Assert.IsEmpty(_fx.Repository.SavedReports());
        Assert.IsEmpty(_fx.Publisher.Published);
    }

    [TestMethod]
    public async Task Auditor_CanRequestGlobal_AndTheScopeIsStoredOnTheServer()
    {
        ReportResponse response = await _fx.Web.RequestAsync(Users.Auditor("aud-1"), ReportFixture.Input("Global"), CancellationToken.None);

        ExpenseReport stored = (await _fx.Repository.FindAsync(response.Id, CancellationToken.None))!;
        Assert.AreEqual(ReportScope.Global, stored.Scope);
        Assert.AreEqual("aud-1", stored.RequesterId);
    }

    [TestMethod]
    public async Task AuditorWithoutEmployeeRole_CannotRequestMine()
    {
        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => _fx.Web.RequestAsync(Users.Auditor("aud-1"), ReportFixture.Input("Mine"), CancellationToken.None));

        Assert.IsEmpty(_fx.Publisher.Published);
    }

    [TestMethod]
    public async Task EmployeeAndAuditor_CanRequestBothScopes()
    {
        CurrentUser both = Users.Create("both-1", Roles.Employee, Roles.Auditor);

        ReportResponse mine = await _fx.Web.RequestAsync(both, ReportFixture.Input("Mine"), CancellationToken.None);
        ReportResponse global = await _fx.Web.RequestAsync(both, ReportFixture.Input("Global"), CancellationToken.None);

        Assert.AreEqual("Mine", mine.Scope);
        Assert.AreEqual("Global", global.Scope);
    }

    [TestMethod]
    public async Task UsersWithoutEmployeeOrAuditor_GetNoAccessToAnyReportOperation()
    {
        ExpenseReport existing = ReportData.Report("emp-1", ReportScope.Mine);
        _fx.Repository.Seed(existing);

        foreach (CurrentUser user in new[]
        {
            Users.Admin("x"), Users.Approver("x"), Users.Finance("x"), Users.WithoutRoles("x"),
            Users.Create("x", Roles.Admin, Roles.Approver, Roles.Finance),
        })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(
                () => _fx.Web.RequestAsync(user, ReportFixture.Input("Mine"), CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ForbiddenException>(
                () => _fx.Web.RequestAsync(user, ReportFixture.Input("Global"), CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ForbiddenException>(
                () => _fx.Web.ListAsync(user, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ForbiddenException>(
                () => _fx.Web.GetAsync(user, existing.Id, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ForbiddenException>(
                () => _fx.Web.GetExcelAsync(user, existing.Id, CancellationToken.None));
        }

        Assert.IsEmpty(_fx.Publisher.Published);
    }

    [TestMethod]
    public async Task List_ReturnsOnlyTheRequestsCreatedByTheCaller()
    {
        CurrentUser first = Users.Employee("emp-1");
        CurrentUser second = Users.Employee("emp-2");
        ReportResponse mine = await _fx.Web.RequestAsync(first, ReportFixture.Input("Mine"), CancellationToken.None);
        await _fx.Web.RequestAsync(second, ReportFixture.Input("Mine"), CancellationToken.None);
        await _fx.Web.RequestAsync(Users.Auditor("aud-1"), ReportFixture.Input("Global"), CancellationToken.None);

        IReadOnlyList<ReportResponse> result = await _fx.Web.ListAsync(first, CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual(mine.Id, result[0].Id);
    }

    [TestMethod]
    public async Task List_ForAnAuditor_StillReturnsOnlyTheirOwnRequests()
    {
        CurrentUser auditor = Users.Auditor("aud-1");
        await _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None);
        ReportResponse own = await _fx.Web.RequestAsync(auditor, ReportFixture.Input("Global"), CancellationToken.None);

        IReadOnlyList<ReportResponse> result = await _fx.Web.ListAsync(auditor, CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual(own.Id, result[0].Id);
    }

    [TestMethod]
    public async Task Detail_OfAnotherUsersReport_LooksLikeItDoesNotExist_EvenForAnAuditor()
    {
        ReportResponse theirs = await _fx.Web.RequestAsync(Users.Employee("emp-1"), ReportFixture.Input("Mine"), CancellationToken.None);

        NotFoundException alien = await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _fx.Web.GetAsync(Users.Auditor("aud-1"), theirs.Id, CancellationToken.None));
        NotFoundException other = await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _fx.Web.GetAsync(Users.Employee("emp-2"), theirs.Id, CancellationToken.None));
        NotFoundException missing = await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _fx.Web.GetAsync(Users.Employee("emp-2"), Guid.NewGuid(), CancellationToken.None));

        Assert.AreEqual(missing.Message, alien.Message);
        Assert.AreEqual(missing.Message, other.Message);
    }
}
