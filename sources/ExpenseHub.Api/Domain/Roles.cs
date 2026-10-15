using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Role names known by the application.
/// </summary>
internal static class Roles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Approver = "Approver";
    public const string Finance = "Finance";
    public const string Auditor = "Auditor";

    /// <summary>
    /// Roles allowed to read expenses (the actual scope is decided per role by the service).
    /// </summary>
    public const string ExpenseReaders = Employee + "," + Approver + "," + Finance + "," + Auditor;

    public static IReadOnlyList<string> All { get; } = new[] { Admin, Employee, Approver, Finance, Auditor };
}
