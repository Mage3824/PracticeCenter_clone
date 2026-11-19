// Application-layer DTO for handing a text report to the frontend (BR14 daily report,
// BR12 editing window).

/// <summary>
/// The text report of a finished working day, shaped for the frontend (BR12, BR14).
/// </summary>
public class ReportDto
{
    /// <summary>Identifier of the report.</summary>
    public Guid Id { get; set; }

    // The finished working day this report describes.
    /// <summary>Finished day this report describes.</summary>
    public Guid WorkDayId { get; set; }

    /// <summary>Report text written by the employee.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Moment the report was first written.</summary>
    public DateTime CreatedAt { get; set; }

    // BR12/BR30: set only after an edit (by the employee before his day ends, or by
    // the administrator afterwards).
    /// <summary>Moment of the last edit; null until someone edits the text (BR12, BR30).</summary>
    public DateTime? UpdatedAt { get; set; }
}
