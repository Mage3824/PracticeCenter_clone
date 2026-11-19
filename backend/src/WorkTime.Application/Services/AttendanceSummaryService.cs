using WorkTime.Domain;

/// <summary>Single source of truth for presence state, worked time and break totals (intern #3).</summary>
public sealed class AttendanceSummaryService
{
    private readonly IUserRepository _users;
    private readonly IWorkDayRepository _days;
    private readonly IBreakRepository _breaks;
    private readonly IAbsenceRepository _absences;

    public AttendanceSummaryService(IUserRepository users, IWorkDayRepository days,
        IBreakRepository breaks, IAbsenceRepository absences)
    {
        _users = users;
        _days = days;
        _breaks = breaks;
        _absences = absences;
    }

    public async Task<List<AttendanceRowDto>> GetSummaryAsync(
        DateTime day, Guid departmentId, CancellationToken cancellationToken)
    {
        day = day.Date;
        var users = await _users.GetActiveEmployeesInDepartmentAsync(departmentId, cancellationToken);
        var absences = (await _absences.GetByDateRangeAsync(day, day, cancellationToken)).ToList();
        var rows = new List<AttendanceRowDto>();
        foreach (var user in users.OrderBy(u => u.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var days = await _days.GetForEmployeeInPeriodAsync(user.Id, day, day, cancellationToken);
            var workDay = days.OrderByDescending(d => d.StartTime).FirstOrDefault();
            var absence = absences.FirstOrDefault(a => a.UserId == user.Id);
            var result = new AttendanceRowDto
            {
                EmployeeId = user.Id,
                EmployeeName = user.FullName,
                DepartmentId = user.DepartmentId,
                Role = user.Role.ToString(),
                Date = day.ToString("yyyy-MM-dd"),
                WorkDayId = workDay?.Id,
                StartTime = workDay?.StartTime.ToString(@"hh\:mm"),
                EndTime = workDay?.EndTime?.ToString(@"hh\:mm"),
                BreakLimitExceeded = workDay?.IsBreakLimitExceeded ?? false,
                Status = absence != null ? "absent" : workDay == null ? "notStarted" :
                    workDay.Status == WorkDayStatus.Active ? "working" : "finished",
                AbsenceReason = absence?.Reason.ToString()
            };
            if (workDay != null)
            {
                var relatedBreaks = await _breaks.GetByWorkDayIdAsync(workDay.Id, cancellationToken);
                var now = day == DateTime.Now.Date ? DateTime.Now.TimeOfDay : workDay.StartTime;
                var end = workDay.EndTime ?? now;
                var counted = TimeSpan.Zero;
                foreach (var part in relatedBreaks)
                {
                    result.BreakCount++;
                    var partEnd = part.EndTime ?? end;
                    var duration = partEnd - part.StartTime;
                    if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
                    result.BreakMinutes += (int)Math.Floor(duration.TotalMinutes);
                    // BR8: a completed break shorter than 5 minutes is not deducted.
                    // An active break is deducted from the live worked timer.
                    if (part.EndTime == null || part.CountedInReports)
                        counted += duration;
                    if (part.EndTime == null && workDay.Status == WorkDayStatus.Active && absence == null)
                        result.Status = "onBreak";
                }
                var worked = end - workDay.StartTime - counted;
                result.WorkedMinutes = (int)Math.Max(0, Math.Floor(worked.TotalMinutes));
            }
            rows.Add(result);
        }
        return rows;
    }
}

/// <summary>Wire-friendly attendance summary row; no sensitive user information.</summary>
public sealed class AttendanceRowDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public Guid DepartmentId { get; set; }
    public string Role { get; set; } = "";
    public string Date { get; set; } = "";
    public Guid? WorkDayId { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string Status { get; set; } = "notStarted";
    public string? AbsenceReason { get; set; }
    public int BreakCount { get; set; }
    public int BreakMinutes { get; set; }
    public int WorkedMinutes { get; set; }
    public bool BreakLimitExceeded { get; set; }
}
