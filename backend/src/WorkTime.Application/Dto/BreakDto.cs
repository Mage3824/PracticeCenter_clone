// Application-layer DTO for handing a Break to the frontend (BR14 lists the breaks of
// a day with their start and end times).

/// <summary>
/// A break inside a working day, shaped for the frontend (BR14).
/// </summary>
public class BreakDto
{
    /// <summary>Identifier of the break.</summary>
    public Guid Id { get; set; }

    // The working day this break belongs to (BR6).
    /// <summary>Day this break belongs to (BR6).</summary>
    public Guid WorkDayId { get; set; }

    /// <summary>Time of day the break started.</summary>
    public TimeSpan StartTime { get; set; }

    // Null while the break is still open (BR10: at most one open break per employee).
    /// <summary>Time of day the break ended; null while it is open (BR10).</summary>
    public TimeSpan? EndTime { get; set; }

    // Null while the break is still open.
    /// <summary>Length of the finished break; null while it is open.</summary>
    public TimeSpan? Duration { get; set; }

    // BR8: false for a break shorter than 5 minutes - it is neither subtracted from
    // worked time nor saved in reports.
    /// <summary>false for a break shorter than 5 minutes: excluded from worked time and reports (BR8).</summary>
    public bool CountedInReports { get; set; } = true;
}
