// Domain entities.
// Time-of-day / durations are TimeSpan, dates are DateTime.
// Nullable end times (day not closed yet) are TimeSpan?.

namespace WorkTime.Domain;

/// <summary>
/// An account of the system: credentials, display name, role, department and the
/// lock state built up by failed login attempts (BR24, BR34, BR35).
/// </summary>
public class User
{
    /// <summary>Identifier every other record refers to by (tokens, reports, event log).</summary>
    public Guid Id { get; set; }

    /// <summary>Login of the account; matched exactly on the login screen, never by substring.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Argon2id PHC string (BR34) - the plain password is never stored here.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Display name shown in report rows and administration lists (BR16).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Access level that the authorization policies of the Api layer act on (BR19).</summary>
    public UserRole Role { get; set; }

    /// <summary>Department the account belongs to; scopes report tables and billing periods (BR23.1).</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>false blocks access while the record and its history stay in the system (BR24).</summary>
    public bool IsActive { get; set; }

    /// <summary>Failed logins counted for the current day; reset by a successful login (BR35, BR36).</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>Moment until which logins are refused (15 minute lock, or end of day); null when free.</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>true when the account is blocked until the end of the day and only an administrator unlocks it (BR35).</summary>
    public bool Locked { get; set; }
}

/// <summary>
/// A node of the department tree; report tables and billing periods are grouped by it
/// (BR16, BR19, BR23.1).
/// </summary>
public class Department
{
    /// <summary>Identifier used by users, period closures and child departments.</summary>
    public Guid Id { get; set; }

    /// <summary>Name shown in the department tree and in report headers.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent node of the tree; null for a top-level department.</summary>
    public Guid? ParentId { get; set; }
}

/// <summary>
/// One calendar day of work of one employee: open from "Start work" until its end time
/// is recorded (BR1, BR2, BR3).
/// </summary>
public class WorkDay
{
    /// <summary>Identifier of the day; breaks, reports and comments refer to it.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner of the day - the employee who started it.</summary>
    public Guid UserId { get; set; }

    /// <summary>Calendar date of the day; the time part carries no meaning.</summary>
    public DateTime Date { get; set; }

    /// <summary>Time of day when "Start work" happened.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Time of day the shift ended; null while it is still open (BR2, BR15).</summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>Open, employee-closed, or system-closed state of the day.</summary>
    public WorkDayStatus Status { get; set; }

    /// <summary>false when less than 15 minutes were worked, so the day is absent from the monthly report (BR5).</summary>
    public bool CountedInReports { get; set; } = true;

    // BR9: the total break time of the day passed the 4 hour cap. The system does
    // not reject such a break; it records the fact so the manager is notified and
    // the administrator can review the day.
    /// <summary>true when the day's total break time passed the 4 hour cap (BR9).</summary>
    public bool IsBreakLimitExceeded { get; set; }
}

/// <summary>
/// A break inside a working day, open until its end time is recorded
/// (BR6, BR8, BR9, BR10, BR11).
/// </summary>
public class Break
{
    /// <summary>Identifier of the break.</summary>
    public Guid Id { get; set; }

    /// <summary>Day this break belongs to; a break exists only inside an open day (BR6).</summary>
    public Guid WorkDayId { get; set; }

    /// <summary>Time of day the break started.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Time of day the break ended; null while it is still open (BR10).</summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>Length of the finished break; null while it is still open.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>false for a break shorter than 5 minutes: neither subtracted from worked time nor present in reports (BR8).</summary>
    public bool CountedInReports { get; set; } = true;
}

/// <summary>
/// The text report of a finished working day (BR12, BR14).
/// </summary>
public class Report
{
    /// <summary>Identifier of the report; comments refer to it.</summary>
    public Guid Id { get; set; }

    /// <summary>The finished day this report describes - at most one report per day.</summary>
    public Guid WorkDayId { get; set; }

    /// <summary>Report text written by the employee.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Moment the report was first written.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Moment of the last edit; set only after an edit (BR12, BR30).</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// A manager's comment on a report (BR13); visible to managers and administrators only.
/// </summary>
public class Comment
{
    /// <summary>Identifier of the comment.</summary>
    public Guid Id { get; set; }

    /// <summary>The report being commented on.</summary>
    public Guid ReportId { get; set; }

    /// <summary>The manager who wrote the comment.</summary>
    public Guid ManagerId { get; set; }

    /// <summary>Comment text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Moment the comment was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Non-working status of an employee for one calendar day (BR25, BR27, BR28).
/// </summary>
public class Absence
{
    /// <summary>Identifier of the status record; one record per employee per date.</summary>
    public Guid Id { get; set; }

    /// <summary>The employee the status belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>Calendar date the status covers; the time part carries no meaning.</summary>
    public DateTime Date { get; set; }

    /// <summary>Why the day is non-working.</summary>
    public AbsenceReason Reason { get; set; }

    // BR25: the administrator or manager who set this status, so the employee can
    // be told in the BR27 tooltip whom to contact (BR30 author of the change).
    /// <summary>Author of the current status, named in the BR27 tooltip; null for legacy records.</summary>
    public Guid? CreatedByUserId { get; set; }
}

/// <summary>
/// One entry of the event log (BR29, BR30); visible to the administrator.
/// </summary>
public class AuditLog
{
    /// <summary>Identifier of the log entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Who performed the action.</summary>
    public Guid UserId { get; set; }

    /// <summary>Kind of the change.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Name of the entity type that was changed.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Entity the change applies to; the history of one entity is read by this id.</summary>
    public Guid EntityId { get; set; }

    /// <summary>State before the change; null for a creation.</summary>
    public string? OldValue { get; set; }

    /// <summary>State after the change; null for a deletion.</summary>
    public string? NewValue { get; set; }

    /// <summary>UTC moment the change happened.</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// The closure state of one billing period of one department (BR23.1, BR23.2).
/// </summary>
public class PeriodClosure
{
    /// <summary>Identifier of the closure record.</summary>
    public Guid Id { get; set; }

    // BR23.1: the billing period belongs to one department, so the closure is
    // addressable by department and not only by its own Id (GetActiveForDepartmentAsync
    // can now answer honestly).
    /// <summary>Department whose billing period this closure stands for (BR23.1).</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Calendar year of the month this closure stands for (BR16, BR18 report window).</summary>
    public int Year { get; set; }

    /// <summary>Calendar month (1-12) of the month this closure stands for.</summary>
    public int Month { get; set; }

    /// <summary>true while editing data inside the period is blocked (BR23.1).</summary>
    public bool IsClosed { get; set; }

    /// <summary>Moment the period was closed; null while it is open.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>The administrator who closed the period.</summary>
    public Guid? ClosedBy { get; set; }

    // BR23.2: "Reopen closed period" requires a reason / act number, and the action
    // is logged as an "Emergency correction" by this administrator. Null while the
    // closure has never been reopened.
    /// <summary>Mandatory reason / act number of a reopening (BR23.2); null while never reopened.</summary>
    public string? ReopenReason { get; set; }

    /// <summary>The administrator who reopened the period, logged as "Emergency correction" (BR23.2).</summary>
    public Guid? ReopenedByUserId { get; set; }
}
