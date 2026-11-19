// Application-layer drafts for the administration endpoints of DepartmentService. The wire payload
// (Requests.Management.cs) is handed over field by field: DepartmentService runs the same checks in
// the same order the controller used to run them, so a malformed field is still reported with the
// same code, and only after the identity/parent checks that came before it.

/// <summary>
/// Fields of a new department, taken from the wire payload of the administration endpoint.
/// </summary>
public class CreateDepartmentDraft
{
    /// <summary>Department name; null or blank is a FIELD_MISSING error, not a default.</summary>
    public string? Name { get; set; }

    /// <summary>Parent node of the tree; null creates a top-level department.</summary>
    public Guid? ParentId { get; set; }
}

// Правка отдела: поля, которые не приходят, остаются прежними (частичная правка).
/// <summary>
/// Partial edit of a department: a field the request omits keeps its previous value.
/// </summary>
public class UpdateDepartmentDraft
{
    /// <summary>New department name; null means "leave the name as it is".</summary>
    public string? Name { get; set; }

    /// <summary>New parent node; null means "leave the position in the tree as it is".</summary>
    public Guid? ParentId { get; set; }
}
