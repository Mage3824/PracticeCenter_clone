// Domain-wide business constants used by services and validators.
// Members are compile-time-ish static values of the static class; callers write WorkTimeRules.MinDayDuration.

/// <summary>
/// Business constants shared by services and validators; they carry the numeric thresholds
/// of the rules (BR5, BR8, BR9, BR35, BR36) instead of scattered literals.
/// </summary>
public static class WorkTimeRules
{
    /// <summary>Minimum worked time for a day to enter the reports at all (BR5).</summary>
    public static readonly TimeSpan MinDayDuration = TimeSpan.FromMinutes(15);

    /// <summary>A break shorter than this is not counted and not saved in reports (BR8).</summary>
    public static readonly TimeSpan MinBreakDuration = TimeSpan.FromMinutes(5);

    /// <summary>Cap of total break time per day; passing it flags the day instead of rejecting the break (BR9).</summary>
    public static readonly TimeSpan MaxBreakDuration = TimeSpan.FromHours(4);

    /// <summary>Failed attempts per day after which the account is blocked until the end of the day (BR35).</summary>
    public const int MaxFailedAttempts = 10;

    /// <summary>Wrong passwords after which every next attempt must carry a CAPTCHA (BR36).</summary>
    public const int CaptchaThreshold = 2;
}
