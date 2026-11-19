// Application-layer drafts for the administration endpoints of UserService. The wire payload
// (Requests.Management.cs) is handed over field by field: UserService runs the same checks in
// the same order the controller used to run them, so a malformed field is still reported with
// the same code, and only after the identity/target checks that came before it.

/// <summary>
/// Fields of a new account, taken from the wire payload of the administration endpoint.
/// </summary>
public class CreateUserDraft
{
    /// <summary>Login of the new account; required and unique.</summary>
    public string? Login { get; set; }

    /// <summary>Display name shown in report rows and lists.</summary>
    public string? FullName { get; set; }

    /// <summary>Plain password, hashed immediately and never stored (BR34).</summary>
    public string? Password { get; set; }

    /// <summary>Role name as written in Enums.cs; never an ordinal.</summary>
    public string? Role { get; set; }

    /// <summary>Department the account is placed in.</summary>
    public Guid? DepartmentId { get; set; }
}

// Правка учётной записи: поля, которые не приходят, остаются прежними (частичная правка).
/// <summary>
/// Partial edit of an account: a field the request omits keeps its previous value.
/// </summary>
public class UpdateUserDraft
{
    /// <summary>New role name; null means "leave the role as it is".</summary>
    public string? Role { get; set; }

    /// <summary>New access state; null means "leave access as it is" (BR24).</summary>
    public bool? IsActive { get; set; }

    /// <summary>New department; null means "leave the department as it is".</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>New display name; null means "leave the name as it is".</summary>
    public string? FullName { get; set; }
}
