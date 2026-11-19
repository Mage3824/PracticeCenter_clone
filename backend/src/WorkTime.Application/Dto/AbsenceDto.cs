// Application-layer DTO for handing an Absence status to the frontend (BR25, BR26, BR27).

/// <summary>
/// Absence status of an employee for one calendar day, shaped for the frontend (BR25, BR26, BR27).
/// </summary>
public class AbsenceDto
{
    /// <summary>Identifier of the status record.</summary>
    public Guid Id { get; set; }

    // The employee the status belongs to.
    /// <summary>Employee whose day is marked non-working.</summary>
    public Guid UserId { get; set; }

    /// <summary>Calendar date the status covers.</summary>
    public DateTime Date { get; set; }

    /// <summary>Why the day is non-working.</summary>
    public AbsenceReason Reason { get; set; }

    // BR25: the administrator or manager who set this status, so the BR27 tooltip can
    // tell the employee whom to contact. Null when the record predates that bookkeeping.
    /// <summary>Author of the status, named in the BR27 tooltip; null for legacy records.</summary>
    public Guid? CreatedByUserId { get; set; }
}
