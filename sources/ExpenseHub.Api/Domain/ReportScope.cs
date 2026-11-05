namespace ExpenseHub.Api.Domain;

/// <summary>
/// Which expenses a report covers. The scope is stored with the request and never changes afterwards.
/// </summary>
internal enum ReportScope
{
    Mine = 0,
    Global = 1,
}
