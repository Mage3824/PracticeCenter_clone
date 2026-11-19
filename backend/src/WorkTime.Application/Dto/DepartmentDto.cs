// Application-layer DTO for handing a department to the frontend. A period closure is
// configured per department (BR23.1), so the tree shape is part of the handover.

/// <summary>
/// A department node, shaped for the frontend; the tree shape matters because billing
/// periods are configured per department (BR23.1).
/// </summary>
public class DepartmentDto
{
    /// <summary>Identifier of the department.</summary>
    public Guid Id { get; set; }

    /// <summary>Name shown in the tree.</summary>
    public string Name { get; set; } = string.Empty;

    // Null for a top-level department.
    /// <summary>Parent node; null for a top-level department.</summary>
    public Guid? ParentId { get; set; }
}
