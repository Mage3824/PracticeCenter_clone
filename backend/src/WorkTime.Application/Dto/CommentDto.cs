// Application-layer DTO for handing a manager comment on a report to the frontend.
// BR13: comments are added by a manager and visible only to the manager and the
// administrator, so this DTO is never sent to an employee row of the UI.

/// <summary>
/// A manager comment on a report, shaped for the frontend (BR13). Never sent to an employee.
/// </summary>
public class CommentDto
{
    /// <summary>Identifier of the comment.</summary>
    public Guid Id { get; set; }

    // The report being commented on.
    /// <summary>Report this comment annotates.</summary>
    public Guid ReportId { get; set; }

    // The manager who wrote the comment.
    /// <summary>Author of the comment.</summary>
    public Guid ManagerId { get; set; }

    /// <summary>Comment text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Moment the comment was written.</summary>
    public DateTime CreatedAt { get; set; }
}
