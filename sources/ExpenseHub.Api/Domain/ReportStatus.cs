namespace ExpenseHub.Api.Domain;

/// <summary>
/// Lifecycle of a report request. <see cref="Completed"/> and <see cref="Failed"/> are final.
/// </summary>
internal enum ReportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}
