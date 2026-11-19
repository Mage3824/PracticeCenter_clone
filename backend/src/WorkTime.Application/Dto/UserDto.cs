// Application-layer DTO for handing a user record to the frontend (login screen,
// administration list). PasswordHash is deliberately NOT part of the handover: BR34
// forbids plain-text passwords and the hash itself has no reason to leave the system.

/// <summary>
/// Account record, shaped for the frontend. The password hash is deliberately absent (BR34).
/// </summary>
public class UserDto
{
    /// <summary>Identifier of the account.</summary>
    public Guid Id { get; set; }

    /// <summary>Login used on the login screen.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Display name shown in report rows and lists.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Access level of the account (BR19).</summary>
    public UserRole Role { get; set; }

    /// <summary>Department the account belongs to.</summary>
    public Guid DepartmentId { get; set; }

    // BR24: a deactivated account is preserved but access is blocked.
    /// <summary>false means access is blocked while the record stays in the system (BR24).</summary>
    public bool IsActive { get; set; }

    // BR35/BR36: the UI shows the countdown to the lock and the CAPTCHA prompt.
    /// <summary>Failed logins of the current day, used for the countdown and CAPTCHA prompt (BR35, BR36).</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>Moment until which logins are refused; null when nothing locks the account.</summary>
    public DateTime? LockedUntil { get; set; }

    // True when the account is blocked until the end of the day (10 failed attempts).
    /// <summary>true when the account is blocked until the end of the day (10 failed attempts, BR35).</summary>
    public bool Locked { get; set; }
}
