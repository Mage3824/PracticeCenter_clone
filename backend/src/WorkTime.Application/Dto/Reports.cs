// Application-layer DTOs for report rendering (no repository/service logic here).

/// <summary>
/// Daily summary of one finished working day: span, break list and worked total (BR14).
/// </summary>
public class DailyReport
{
    /// <summary>Time of day the shift started.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Time of day the shift ended; null for an incomplete day, which renders no report (BR15).</summary>
    public TimeSpan? EndTime { get; set; }

    // Список перерывов, а не словарь
    /// <summary>Breaks of the day in start-time order, listed even when they are not counted (BR8).</summary>
    public List<ReportBreak> Breaks { get; set; } = new();

    /// <summary>Worked time: day span minus the counted breaks (BR4, BR11).</summary>
    public TimeSpan TotalHours { get; set; }
}

/// <summary>
/// One break line of a daily report (BR14).
/// </summary>
public class ReportBreak
{
    /// <summary>Time of day the break started.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Time of day the break ended; null while it stayed open.</summary>
    public TimeSpan? EndTime { get; set; }
}

/// <summary>
/// One row of the monthly table: an employee and his hours per calendar day (BR16).
/// </summary>
public class MonthlyReport
{
    /// <summary>Display name of the employee this row belongs to.</summary>
    public string EmployeeName { get; set; } = string.Empty;

    // Инициализация словаря
    /// <summary>Worked hours per calendar day; keys are dates with a zero time part (BR16).</summary>
    public Dictionary<DateTime, TimeSpan> HoursByDay { get; set; } = new();

    /// <summary>Total worked hours of the month across the counted days.</summary>
    public TimeSpan TotalHours { get; set; }

    // BR26: days that carry an "Absence" status are never counted as hours; the
    // frontend renders them as their own column. Calendar dates only (time part is zero).
    /// <summary>Days carrying an absence status, listed as their own column instead of hours (BR26).</summary>
    public List<DateTime> AbsenceDays { get; set; } = new();
}
