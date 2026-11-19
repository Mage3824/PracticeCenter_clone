// Application-layer DTO returned by AuthService.LoginAsync (BR35, BR36).
// Message carries the human-readable notification: the countdown to the lock, the
// CAPTCHA prompt, or the reason of a blocked account. It is optional information;
// callers that only care about the three flags can ignore it.
using WorkTime.Domain;

/// <summary>
/// Outcome of a login attempt: success, CAPTCHA requirement, lock state and the text the
/// UI should show (BR35, BR36). Never thrown - a failed login is data, not an error.
/// </summary>
public class AuthResult
{
    /// <summary>true when the credentials were accepted and the session may be issued.</summary>
    public bool IsSuccess { get; set; }

    // BR36: after 2 wrong passwords every next attempt must supply a CAPTCHA.
    /// <summary>true when the next attempt must carry a CAPTCHA (BR36).</summary>
    public bool RequiresCaptcha { get; set; }

    // BR35: account is temporarily locked (15 minutes) or blocked until the end of
    // the day after 10 failed attempts.
    /// <summary>true when the account is locked or blocked and the attempt was refused (BR35).</summary>
    public bool IsLocked { get; set; }

    /// <summary>Notification text for the UI: countdown, CAPTCHA prompt or blocking reason.</summary>
    public string Message { get; set; } = string.Empty;

    // Moment from which a lock stops blocking the next attempt (null when no lock).
    /// <summary>Moment from which logins are allowed again; null when nothing locks the account.</summary>
    public DateTime? LockedUntil { get; set; }

    // Identity of the account that logged in, taken from the User AuthService resolved.
    // The access token of a successful login names this Guid as its subject, so the Api
    // layer never has to look the user up itself; Guid.Empty means "no identity" (404).
    /// <summary>Subject the access token names; Guid.Empty when no account was resolved.</summary>
    public Guid UserId { get; set; }

    /// <summary>Role of the account that logged in, so the Api layer can apply the BR19 policies.</summary>
    public UserRole Role { get; set; }
}
