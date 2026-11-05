using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.TestSupport;

/// <summary>
/// In-memory report store that behaves like a database: only what was saved survives, and a failing save
/// rolls every tracked request back to its last saved state. Tests can therefore tell "persisted" from "in memory".
/// </summary>
internal sealed class InMemoryReportRepository : IReportRepository
{
    private readonly Dictionary<Guid, ExpenseReport> _tracked = new();
    private readonly Dictionary<Guid, Dictionary<string, object?>> _saved = new();
    private readonly List<ExpenseReport> _staged = new();

    public int SaveCount { get; private set; }

    /// <summary>
    /// One entry per save: null lets it succeed, an exception makes it fail. Saves beyond the script succeed.
    /// </summary>
    public Queue<Exception?> SaveScript { get; } = new();

    public void Seed(ExpenseReport report)
    {
        _tracked[report.Id] = report;
        _saved[report.Id] = Capture(report);
    }

    public bool IsSaved(Guid id)
    {
        return _saved.ContainsKey(id);
    }

    public ReportStatus SavedStatus(Guid id)
    {
        return (ReportStatus)_saved[id][nameof(ExpenseReport.Status)]!;
    }

    public string? SavedResult(Guid id)
    {
        return (string?)_saved[id][nameof(ExpenseReport.ResultJson)];
    }

    public int SavedAttempts(Guid id)
    {
        return (int)_saved[id][nameof(ExpenseReport.Attempts)]!;
    }

    public DateTime? SavedStartedAt(Guid id)
    {
        return (DateTime?)_saved[id][nameof(ExpenseReport.StartedAtUtc)];
    }

    public IReadOnlyList<ExpenseReport> SavedReports()
    {
        return _tracked.Values.Where(report => _saved.ContainsKey(report.Id)).ToList();
    }

    public Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        _tracked.TryGetValue(id, out ExpenseReport? report);

        return Task.FromResult(report is not null && _saved.ContainsKey(id) ? report : null);
    }

    public Task<IReadOnlyList<ExpenseReport>> ListByRequesterAsync(string requesterId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseReport> result = SavedReports()
            .Where(report => report.RequesterId == requesterId)
            .OrderByDescending(report => report.CreatedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public void Add(ExpenseReport report)
    {
        _staged.Add(report);
        _tracked[report.Id] = report;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Exception? failure = SaveScript.Count > 0 ? SaveScript.Dequeue() : null;
        if (failure is not null)
        {
            Rollback();

            throw failure;
        }

        SaveCount++;
        foreach (ExpenseReport report in _tracked.Values)
        {
            _saved[report.Id] = Capture(report);
        }

        _staged.Clear();

        return Task.CompletedTask;
    }

    private static Dictionary<string, object?> Capture(ExpenseReport report)
    {
        return Settable().ToDictionary(property => property.Name, property => property.GetValue(report));
    }

    private static IEnumerable<PropertyInfo> Settable()
    {
        return typeof(ExpenseReport).GetProperties().Where(property => property.GetSetMethod(true) is not null);
    }

    private void Rollback()
    {
        foreach (ExpenseReport staged in _staged)
        {
            _tracked.Remove(staged.Id);
        }

        _staged.Clear();

        foreach (ExpenseReport report in _tracked.Values)
        {
            foreach (PropertyInfo property in Settable())
            {
                property.GetSetMethod(true)!.Invoke(report, new[] { _saved[report.Id][property.Name] });
            }
        }
    }
}
