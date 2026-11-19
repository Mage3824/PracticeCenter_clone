// Dev-tier ("in-memory") repositories for the WorkTime.Api (Stage 2) - see DevRepositories.Users.cs
// for the layer note.
using WorkTime.Domain;

namespace WorkTime.Infrastructure;

// In-memory implementation of IReportRepository. No cross-repository lookups: Report is addressed
// by its own Id and by the WorkDayId it describes, both stored on the entity itself.
/// <summary>
/// Dev-tier (in-memory) implementation of <see cref="IReportRepository"/>. A report is addressed by its own
/// Id and by the work day it describes, so no cross-repository lookup is needed.
/// </summary>
public class InMemoryReportRepository : IReportRepository
{
    private readonly Dictionary<Guid, Report> _reports = new();

    /// <inheritdoc />
    public Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _reports.TryGetValue(id, out var report);
        return Task.FromResult(report);
    }

    // Requested Ids are taken from the keys of "ids"; a missing Id is simply absent from the result.
    /// <inheritdoc />
    public Task<Dictionary<Guid, Report>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Report>();

        foreach (var id in ids.Keys)
        {
            _reports.TryGetValue(id, out var report);
            if (report != null)
            {
                result[report.Id] = report;
            }
        }

        return Task.FromResult(result);
    }

    // BR14: at most one report per working day, so the service can find and update it without
    // being handed a report Id. Null when the day has no report yet.
    /// <inheritdoc />
    public Task<Report?> GetForWorkDayAsync(Guid workDayId, CancellationToken cancellationToken)
    {
        foreach (var entry in _reports)
        {
            if (entry.Value.WorkDayId == workDayId)
            {
                return Task.FromResult<Report?>(entry.Value);
            }
        }

        return Task.FromResult((Report?)null);
    }

    /// <inheritdoc />
    public Task SaveAsync(Report report, CancellationToken cancellationToken)
    {
        _reports[report.Id] = report;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _reports.Remove(id);
        return Task.CompletedTask;
    }
    // Возвращает копию списка отчётов
    public Task<IEnumerable<Report>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        IEnumerable<Report> result = _reports.Values.ToList();
        return Task.FromResult(result);
    }
}

// In-memory implementation of ICommentRepository. Comment stores ReportId (not WorkDayId), so
// "comments of a work day" is resolved through an injected report repository:
// Comment.ReportId -> Report.WorkDayId (DI style).
/// <summary>
/// Dev-tier (in-memory) implementation of <see cref="ICommentRepository"/>. Comments of a work day are
/// resolved through the injected <see cref="IReportRepository"/> (Comment.ReportId -> Report.WorkDayId).
/// </summary>
public class InMemoryCommentRepository : ICommentRepository
{
    private readonly Dictionary<Guid, Comment> _comments = new();

    // Resolves Comment.ReportId to the work day that owns that report.
    private readonly IReportRepository _reports;

    /// <summary>Creates the repository with the report lookup it delegates to.</summary>
    /// <param name="reports">Resolves Comment.ReportId to the work day that owns that report.</param>
    public InMemoryCommentRepository(IReportRepository reports)
    {
        _reports = reports;
    }

    /// <inheritdoc />
    public Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _comments.TryGetValue(id, out var comment);
        return Task.FromResult(comment);
    }

    // Requested Ids are taken from the keys of "ids"; a missing Id is simply absent from the result.
    /// <summary>
    /// Batch fetch of comments by id; found comments keyed by id, missing ids simply absent.
    /// A batch primitive of this dev-tier class - <see cref="ICommentRepository"/> itself addresses
    /// comments by id or by their report.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found comments keyed by id.</returns>
    public Task<Dictionary<Guid, Comment>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Comment>();

        foreach (var id in ids.Keys)
        {
            _comments.TryGetValue(id, out var comment);
            if (comment != null)
            {
                result[comment.Id] = comment;
            }
        }

        return Task.FromResult(result);
    }

    // Comments of every report that belongs to the given working day.
    /// <inheritdoc />
    public async Task<Dictionary<Guid, Comment>> GetAllForWorkDayAsync(
        Guid workDayId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Comment>();

        foreach (var entry in _comments)
        {
            var comment = entry.Value;
            var report = await _reports.GetByIdAsync(comment.ReportId, cancellationToken);
            if (report != null && report.WorkDayId == workDayId)
            {
                result[comment.Id] = comment;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public Task SaveAsync(Comment comment, CancellationToken cancellationToken)
    {
        _comments[comment.Id] = comment;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _comments.Remove(id);
        return Task.CompletedTask;
    }
}
