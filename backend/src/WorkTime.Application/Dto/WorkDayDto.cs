// Application-layer DTO for handing a WorkDay to the frontend. A flat mirror of the
// domain entity: Guid ids travel as their string form, TimeSpan / DateTime as plain
// scalars, so no repository or service logic is reachable through this type.

/// <summary>
/// A working day, shaped for the frontend as a flat mirror of the domain entity.
/// </summary>
public class WorkDayDto
{
    /// <summary>Identifier of the day.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner of the day.</summary>
    public Guid UserId { get; set; }

    /// <summary>Calendar date of the day.</summary>
    public DateTime Date { get; set; }

    /// <summary>Time of day "Start work" happened.</summary>
    public TimeSpan StartTime { get; set; }

    // Null while the day is still open (BR2/BR15).
    /// <summary>Time of day the shift ended; null while it is open (BR2, BR15).</summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>Open, employee-closed, or system-closed state of the day.</summary>
    public WorkDayStatus Status { get; set; }

    // BR5: false when less than 15 minutes were worked, so the day is absent from
    // the monthly report.
    /// <summary>false when less than 15 minutes were worked, so the day is absent from reports (BR5).</summary>
    public bool CountedInReports { get; set; } = true;

    // BR9: total break time of this day passed the 4 hour cap - the manager is
    // notified through this flag instead of a rejected action.
    /// <summary>true when the day's break total passed the 4 hour cap; a notification, not a rejection (BR9).</summary>
    public bool IsBreakLimitExceeded { get; set; }
}
