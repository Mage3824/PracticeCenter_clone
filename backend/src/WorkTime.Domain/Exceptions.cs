// Domain-level exception hierarchy: business-rule violations and missing entities.

/// <summary>
/// A business rule (BRxx) was violated. Thrown by Application services and mapped by the
/// Api layer to the matching HTTP response.
/// </summary>
public class BusinessRuleException : Exception
{
    /// <summary>Rule identifier carried to the caller, for example "BR23.1".</summary>
    public string Code { get; set; }

    /// <summary>
    /// Creates a violation of one business rule.
    /// </summary>
    /// <param name="code">Rule identifier the message refers to (BRxx).</param>
    /// <param name="message">Human-readable reason shown to the user.</param>
    public BusinessRuleException(string code, string message) : base(message)
    {
        this.Code = code;
    }
}

/// <summary>
/// The entity a caller asked for does not exist (404 path).
/// </summary>
public class EntityNotFoundException : Exception
{
    /// <summary>Id that was searched for.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Name of the entity type that was searched for.</summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Creates a "not found" error naming the entity the caller asked for.
    /// </summary>
    /// <param name="entityId">Id that was searched for.</param>
    /// <param name="entityType">Name of the entity type that was searched for.</param>
    public EntityNotFoundException(Guid entityId, string entityType) 
        : base($"Entity of type '{entityType}' with id '{entityId}' was not found.")
    {
        this.EntityId = entityId;
        this.EntityType = entityType;
    }
}
