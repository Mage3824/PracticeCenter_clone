// Application-layer DTO for handing an event-log entry to the frontend. BR29: logs are
// visible only to the administrator; BR30: a change carries the previous value, the new
// value, the author and the time.

/// <summary>
/// One entry of the event log, shaped for the frontend (BR29, BR30).
/// </summary>
public class AuditLogDto
{
    /// <summary>Identifier of the log entry.</summary>
    public Guid Id { get; set; }

    // Who performed the action.
    /// <summary>Actor who performed the action.</summary>
    public Guid UserId { get; set; }

    /// <summary>Kind of the change.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Name of the entity type that was changed.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Entity the change applies to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>State before the change; null for a creation.</summary>
    public string? OldValue { get; set; }

    /// <summary>State after the change; null for a deletion.</summary>
    public string? NewValue { get; set; }

    /// <summary>UTC moment the change happened.</summary>
    public DateTime Timestamp { get; set; }
}
