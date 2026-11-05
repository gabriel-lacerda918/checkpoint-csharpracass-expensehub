using System;
using ExpenseHub.Api.Application.Exceptions;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application.Reports;

/// <summary>
/// Who may request which scope and whose requests may be read. The requester always comes from the token.
/// Employee requests Mine, Auditor requests Global, and everybody reads only the requests they created.
/// </summary>
internal static class ReportAccessPolicy
{
    public static void EnsureCanUseReports(CurrentUser user)
    {
        AccessGuard.RequireAnyRole(user, Roles.Employee, Roles.Auditor);
    }

    public static ReportScope AuthorizeScope(CurrentUser user, string? scope)
    {
        ReportScope parsed = scope switch
        {
            "Mine" => ReportScope.Mine,
            "Global" => ReportScope.Global,
            _ => throw new ValidationFailedException("Scope", "Scope must be Mine or Global."),
        };

        string requiredRole = parsed == ReportScope.Mine ? Roles.Employee : Roles.Auditor;
        if (!user.IsInRole(requiredRole))
        {
            throw new ForbiddenException($"The {requiredRole} role is required to request the {parsed} scope.");
        }

        return parsed;
    }

    /// <summary>
    /// A request created by someone else looks exactly like one that does not exist, even for an Auditor.
    /// </summary>
    public static void EnsureOwner(CurrentUser user, ExpenseReport report)
    {
        if (!string.Equals(report.RequesterId, user.UserId, StringComparison.Ordinal))
        {
            throw new NotFoundException("Report not found.");
        }
    }
}
