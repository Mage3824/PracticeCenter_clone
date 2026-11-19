// Shared enumerations for the WorkTime domain.
// Standard C# (.NET 8) - no external dependencies.

/// <summary>
/// Access level of an account; the authorization policies of the Api layer act on it (BR19).
/// </summary>
public enum UserRole
{
    /// <summary>Full administration: users, departments, periods, unlocking accounts.</summary>
    Admin,

    /// <summary>Sees the reports of his department and may set the absence status.</summary>
    Manager,

    /// <summary>Sees and edits only his own working day and his own reports.</summary>
    Employee,
}

/// <summary>
/// Lifecycle state of a working day (BR1, BR2, BR3).
/// </summary>
public enum WorkDayStatus
{
    /// <summary>Shift is open: no end time recorded yet.</summary>
    Active,

    /// <summary>Closed by the employee through "Finish work".</summary>
    Finished,

    /// <summary>Closed by the system at 23:59:59, logged as an "Auto-finish" event (BR3).</summary>
    AutoFinished,
}

/// <summary>
/// Why a calendar day is non-working for an employee (BR25, BR27).
/// </summary>
public enum AbsenceReason
{
    /// <summary>Illness.</summary>
    Sick,

    /// <summary>Vacation / leave.</summary>
    Leave,

    /// <summary>Planned non-working day.</summary>
    DayOff,

    /// <summary>Absent without a documented reason.</summary>
    Absence,
}

/// <summary>
/// Kind of a change recorded in the event log (BR29, BR30).
/// </summary>
public enum AuditAction
{
    /// <summary>Entity was created.</summary>
    Create,

    /// <summary>Entity was modified.</summary>
    Update,

    /// <summary>Entity was removed.</summary>
    Delete,
}
