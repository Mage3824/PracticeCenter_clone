// Repository interfaces (Application layer).
// Every method is asynchronous and takes an explicit CancellationToken.
//
// Addressing rule: a repository answers a question about *criteria* (an id, a date,
// a period, an owner) and returns whatever it finds. Callers never assemble the
// answer themselves, so no service method accepts a list of entity ids that only
// the caller knows. GetAllByIdsAsync stays available as a batch-fetch primitive for
// flows that already hold concrete ids (for example bulk administration actions).
using WorkTime.Domain;

/// <summary>
/// Storage of accounts: lookup by id, name and login, the department roster, and the
/// write path used by administration and by the login counters.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Account by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The account, or null.</returns>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of accounts by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found accounts keyed by id; missing ids are simply absent.</returns>
    Task<Dictionary<Guid, User>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Active accounts matching a display name.
    /// </summary>
    /// <param name="name">Display name to search for.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Matching active accounts keyed by name.</returns>
    Task<Dictionary<String, User>> GetActiveByNameAsync(String name, CancellationToken cancellationToken);

    // Exact login match (no substring search), for the login screen.
    /// <summary>
    /// Account with an exact login, or null when there is no such account.
    /// </summary>
    /// <param name="login">Login to match exactly.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The account, or null.</returns>
    Task<User?> GetByLoginAsync(String login, CancellationToken cancellationToken);

    // Active users of one department (BR16 rows are chosen by the repository, never
    // assembled by a caller). Guid.Empty means "every active user of the system".
    /// <summary>
    /// Active accounts of one department - the BR16 rows of a report table, chosen by
    /// the repository rather than assembled by the caller.
    /// </summary>
    /// <param name="departmentId">Department to filter by; Guid.Empty means every active account of the system.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Active accounts of the department (or of the whole system).</returns>
    Task<IEnumerable<User>> GetActiveEmployeesInDepartmentAsync(
        Guid departmentId,
        CancellationToken ct = default);

// Stage 2 administration needs the whole account list: BR24 keeps deactivated accounts
    // visible, so a read-all path belongs to the repository contract.
    /// <summary>
    /// Every account, deactivated ones included - administration needs them because
    /// BR24 keeps deactivated records in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>All accounts.</returns>
    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores the account as it is now (insert or update).
    /// </summary>
    /// <param name="user">Account to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the account permanently.
    /// </summary>
    /// <param name="id">Identifier of the account to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of the department tree: nodes by id, children of a node, and the whole tree.
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// Department by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the department.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The department, or null.</returns>
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of departments by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found departments keyed by id.</returns>
    Task<Dictionary<Guid, Department>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    // Departments directly under the given parent (the hierarchy walk for BR16/BR19 grouping).
    /// <summary>
    /// Departments directly under one parent - the hierarchy walk for BR16/BR19 grouping.
    /// </summary>
    /// <param name="parentId">Parent node whose children are wanted.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Child departments of the parent.</returns>
    Task<IEnumerable<Department>> GetChildrenOfAsync(Guid parentId, CancellationToken ct = default);

// The administration controller enumerates the department tree; without this the top-level
    // departments (ParentId = null) are unreachable through GetChildrenOfAsync.
    /// <summary>
    /// Every department, top-level ones included - without this, nodes with no parent
    /// are unreachable through <see cref="GetChildrenOfAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>All departments.</returns>
    Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores the department as it is now (insert or update).
    /// </summary>
    /// <param name="department">Department to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(Department department, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the department permanently.
    /// </summary>
    /// <param name="id">Identifier of the department to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of working days: lookup by id, per-employee reads and the reads of a
/// calendar period that report rendering needs.
/// </summary>
public interface IWorkDayRepository
{
    /// <summary>
    /// Working day by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the day.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The day, or null.</returns>
    Task<WorkDay?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of working days by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found days keyed by id.</returns>
    Task<Dictionary<Guid, WorkDay>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    // Finished days (EndTime set and Status not Active) whose calendar date falls
    // inside the inclusive window [startDate, endDate]. The window is how a service
    // asks for "the billing period" without receiving day ids.
    /// <summary>
    /// Finished days whose calendar date falls inside the inclusive window - the way a
    /// service asks for "the billing period" without being handed day ids.
    /// </summary>
    /// <param name="startDate">First date of the window, inclusive.</param>
    /// <param name="endDate">Last date of the window, inclusive.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Finished days inside the window.</returns>
    Task<IEnumerable<WorkDay>> GetFinishedDaysForPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    // Every working day of one employee (open or finished) inside the inclusive
    // window [startDate, endDate].
    /// <summary>
    /// Every day of one employee - open or finished - inside the inclusive window.
    /// </summary>
    /// <param name="employeeId">Owner of the days.</param>
    /// <param name="startDate">First date of the window, inclusive.</param>
    /// <param name="endDate">Last date of the window, inclusive.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Days of the employee inside the window.</returns>
    Task<IEnumerable<WorkDay>> GetForEmployeeInPeriodAsync(Guid employeeId, DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>
    /// Open days of one employee, addressed through a period closure.
    /// </summary>
    /// <param name="employeeId">Owner of the days.</param>
    /// <param name="periodClosureId">Closure the days are grouped under.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Open days keyed by id.</returns>
    Task<Dictionary<Guid, WorkDay>> GetActiveForEmployeeAsync(Guid employeeId, Guid periodClosureId, CancellationToken cancellationToken);

    // Open (not yet finished) working days of one employee on one calendar date. This is
    // the BR1 question - "is a shift already open for this employee today?" - and it
    // answers it without any help from a period closure.
    /// <summary>
    /// Open days of one employee on one calendar date - the BR1 question "is a shift
    /// already open for this employee today?", answered without a period closure.
    /// </summary>
    /// <param name="employeeId">Owner of the day.</param>
    /// <param name="date">Calendar date to check.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Open days keyed by id; empty when none.</returns>
    Task<Dictionary<Guid, WorkDay>> GetActiveForEmployeeOnDateAsync(
        Guid employeeId,
        DateTime date,
        CancellationToken ct = default);

    /// <summary>
    /// Stores the working day as it is now (insert or update).
    /// </summary>
    /// <param name="workDay">Day to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(WorkDay workDay, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the working day permanently.
    /// </summary>
    /// <param name="id">Identifier of the day to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of breaks inside a working day, including the per-day read that lets a
/// service total a day's breaks without being handed their ids.
/// </summary>
public interface IBreakRepository
{
    /// <summary>
    /// Break by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the break.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The break, or null.</returns>
    Task<Break?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of breaks by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found breaks keyed by id.</returns>
    Task<Dictionary<Guid, Break>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Breaks of one employee that are still open (BR10 allows at most one).
    /// </summary>
    /// <param name="employeeId">Owner of the breaks.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Open breaks keyed by id.</returns>
    Task<Dictionary<Guid, Break>> GetUnfinishedForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);

    // Every break of one working day, open or finished. This is the query that lets
    // a service total and list a day's breaks without being handed their ids.
    /// <summary>
    /// Every break of one working day, open or finished - the query that lets a service
    /// total and list them without being handed ids.
    /// </summary>
    /// <param name="workDayId">Day whose breaks are wanted.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Breaks of the day.</returns>
    Task<IEnumerable<Break>> GetByWorkDayIdAsync(Guid workDayId, CancellationToken ct = default);

    /// <summary>
    /// Stores the break as it is now (insert or update).
    /// </summary>
    /// <param name="workBreak">Break to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(Break workBreak, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the break permanently.
    /// </summary>
    /// <param name="id">Identifier of the break to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of the text reports of finished days, addressed by the day rather than by
/// a report id the caller invents.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Report by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the report.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The report, or null.</returns>
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of reports by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found reports keyed by id.</returns>
    Task<Dictionary<Guid, Report>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    // The report of a working day (at most one per day), so a service can find and
    // update it without being handed a report id.
    /// <summary>
    /// The report of a working day - at most one per day - so a service can find and
    /// update it without being handed a report id.
    /// </summary>
    /// <param name="workDayId">Day whose report is wanted.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>The report of the day, or null.</returns>
    Task<Report?> GetForWorkDayAsync(Guid workDayId, CancellationToken ct = default);

    /// <summary>
    /// Stores the report as it is now (insert or update).
    /// </summary>
    /// <param name="report">Report to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(Report report, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the report permanently.
    /// </summary>
    /// <param name="id">Identifier of the report to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    // Получить все сохранённые отчёты
    Task<IEnumerable<Report>> GetAllAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Storage of manager comments on reports (BR13).
/// </summary>
public interface ICommentRepository
{
    /// <summary>
    /// Comment by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the comment.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The comment, or null.</returns>
    Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Comments of one working day, keyed by comment id.
    /// </summary>
    /// <param name="workDayId">Day whose comments are wanted.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Comments keyed by id.</returns>
    Task<Dictionary<Guid, Comment>> GetAllForWorkDayAsync(Guid workDayId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the comment.
    /// </summary>
    /// <param name="comment">Comment to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(Comment comment, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the comment permanently.
    /// </summary>
    /// <param name="id">Identifier of the comment to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of absence statuses, addressable by employee, date and period closure.
/// </summary>
public interface IAbsenceRepository
{
    /// <summary>
    /// Absence record by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the record.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The record, or null.</returns>
    Task<Absence?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of absence records by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found records keyed by id.</returns>
    Task<Dictionary<Guid, Absence>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Absence records grouped under one period closure.
    /// </summary>
    /// <param name="periodClosureId">Closure the records belong to.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Records keyed by id.</returns>
    Task<Dictionary<Guid, Absence>> GetForPeriodClosureAsync(Guid periodClosureId, CancellationToken cancellationToken);

    // Absences dated inside the inclusive window [startDate, endDate] of any employee.
    /// <summary>
    /// Absence records of any employee dated inside the inclusive window - the BR26
    /// source of a monthly report.
    /// </summary>
    /// <param name="startDate">First date of the window, inclusive.</param>
    /// <param name="endDate">Last date of the window, inclusive.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Records inside the window.</returns>
    Task<IEnumerable<Absence>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    // Absences of one employee dated on one calendar date (BR26/BR27 lookup).
    /// <summary>
    /// Absence records of one employee on one calendar date - the BR26/BR27 lookup and
    /// the BR27 start-of-day guard.
    /// </summary>
    /// <param name="employeeId">Employee whose records are wanted.</param>
    /// <param name="date">Calendar date to look up.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Records for that employee and date.</returns>
    Task<IEnumerable<Absence>> GetForEmployeeOnDateAsync(Guid employeeId, DateTime date, CancellationToken ct = default);

    /// <summary>
    /// Stores the absence record as it is now (insert or update).
    /// </summary>
    /// <param name="absence">Record to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(Absence absence, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the absence record permanently.
    /// </summary>
    /// <param name="id">Identifier of the record to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of event-log entries, with the per-entity history the BR30 audit needs.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Log entry by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the entry.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The entry, or null.</returns>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of log entries by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found entries keyed by id.</returns>
    Task<Dictionary<Guid, AuditLog>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    // BR30: the change history of one entity (who changed what, in which order), so a
    // service can prove that an action was logged without being handed log ids.
    /// <summary>
    /// Change history of one entity - who changed what, in which order - so a service
    /// can prove an action was logged without being handed log ids (BR30).
    /// </summary>
    /// <param name="entityId">Entity whose history is wanted.</param>
    /// <param name="ct">Cancels the storage call.</param>
    /// <returns>Entries for that entity.</returns>
    Task<IEnumerable<AuditLog>> GetHistoryForEntityAsync(Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Appends a log entry.
    /// </summary>
    /// <param name="auditLog">Entry to append.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(AuditLog auditLog, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a log entry permanently.
    /// </summary>
    /// <param name="id">Identifier of the entry to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Storage of billing-period closures, addressable by department and by calendar month.
/// </summary>
public interface IPeriodClosureRepository
{
    /// <summary>
    /// Closure by id, or null when it does not exist.
    /// </summary>
    /// <param name="id">Identifier of the closure.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The closure, or null.</returns>
    Task<PeriodClosure?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Batch fetch of closures by id.
    /// </summary>
    /// <param name="ids">Ids to fetch, keyed by id.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Found closures keyed by id.</returns>
    Task<Dictionary<Guid, PeriodClosure>> GetAllByIdsAsync(Dictionary<Guid, Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// The currently open closure of one department.
    /// </summary>
    /// <param name="departmentId">Department whose closure is wanted.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The open closure, or null when the department has none.</returns>
    Task<PeriodClosure?> GetActiveForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken);

    // Every closure this department ever had (open and closed), so the administrator can
    // find the closed one that BR23.2 has to reopen.
    /// <summary>
    /// Every closure of one department, open and closed - so the administrator can find
    /// the closed one that BR23.2 has to reopen.
    /// </summary>
    /// <param name="departmentId">Department whose closures are wanted.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>Closures keyed by id.</returns>
    Task<Dictionary<Guid, PeriodClosure>> GetAllForDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken);

    // The closure standing for one calendar month of one department (BR18 export of an
    // arbitrary period). Null when the department has no closure for that month.
    /// <summary>
    /// The closure standing for one calendar month of one department - the BR18 export
    /// of an arbitrary period.
    /// </summary>
    /// <param name="departmentId">Department whose closure is wanted.</param>
    /// <param name="year">Calendar year of the month.</param>
    /// <param name="month">Calendar month (1-12).</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    /// <returns>The closure, or null when the department has none for that month.</returns>
    Task<PeriodClosure?> GetForDepartmentOnMonthAsync(
        Guid departmentId,
        int year,
        int month,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores the closure as it is now (insert or update).
    /// </summary>
    /// <param name="periodClosure">Closure to store.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task SaveAsync(PeriodClosure periodClosure, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the closure permanently.
    /// </summary>
    /// <param name="id">Identifier of the closure to remove.</param>
    /// <param name="cancellationToken">Cancels the storage call.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
