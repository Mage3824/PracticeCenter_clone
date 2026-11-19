// Application-layer DTO for handing the billing-period closure state to the frontend
// (BR23.1 closing of the period, BR23.2 reopening by the administrator).

/// <summary>
/// Closure state of one billing period of one department, shaped for the frontend
/// (BR23.1, BR23.2).
/// </summary>
public class PeriodClosureDto
{
    /// <summary>Identifier of the closure record.</summary>
    public Guid Id { get; set; }

    // BR23.1: the department whose billing period this closure stands for.
    /// <summary>Department this closure stands for (BR23.1).</summary>
    public Guid DepartmentId { get; set; }

    // The calendar month this closure stands for (BR16/BR18 report window).
    /// <summary>Calendar year of the month the closure stands for (BR16/BR18 report window).</summary>
    public int Year { get; set; }

    /// <summary>Calendar month (1-12) the closure stands for.</summary>
    public int Month { get; set; }

    /// <summary>true while editing data inside the period is blocked (BR23.1).</summary>
    public bool IsClosed { get; set; }

    /// <summary>Moment the period was closed; null while it is open.</summary>
    public DateTime? ClosedAt { get; set; }

    // The administrator who closed the period.
    /// <summary>Administrator who closed the period.</summary>
    public Guid? ClosedBy { get; set; }

    // BR23.2: the mandatory reason / act number given when a closed period was
    // reopened. Null while the period has never been reopened.
    /// <summary>Reason / act number recorded at reopening (BR23.2); null while never reopened.</summary>
    public string? ReopenReason { get; set; }

    // BR23.2: the administrator who reopened the period ("Emergency correction").
    /// <summary>Administrator who reopened the period, logged as "Emergency correction" (BR23.2).</summary>
    public Guid? ReopenedByUserId { get; set; }
}
