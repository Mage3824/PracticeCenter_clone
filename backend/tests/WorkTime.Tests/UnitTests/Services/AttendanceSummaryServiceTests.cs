using WorkTime.Domain;

namespace WorkTime.Tests.UnitTests.Services;

/// <summary>Intern #3: presence aggregation, break transitions and absence priority.</summary>
public sealed class AttendanceSummaryServiceTests
{
    private readonly WorkTime.Infrastructure.InMemoryUserRepository _users = new();
    private readonly WorkTime.Infrastructure.InMemoryPeriodClosureRepository _periods = new();
    private readonly WorkTime.Infrastructure.InMemoryWorkDayRepository _days;
    private readonly WorkTime.Infrastructure.InMemoryBreakRepository _breaks;
    private readonly WorkTime.Infrastructure.InMemoryAbsenceRepository _absences;
    private readonly Guid _department = Guid.NewGuid();
    private readonly DateTime _date = DateTime.Today;
    private readonly CancellationToken _ct = CancellationToken.None;

    public AttendanceSummaryServiceTests()
    {
        _days = new WorkTime.Infrastructure.InMemoryWorkDayRepository(_periods);
        _breaks = new WorkTime.Infrastructure.InMemoryBreakRepository(_days);
        _absences = new WorkTime.Infrastructure.InMemoryAbsenceRepository(_periods);
    }

    private AttendanceSummaryService Service() => new(_users, _days, _breaks, _absences);

    private async Task<User> AddUser(string name = "Иван Петров")
    {
        var user = new User { Id = Guid.NewGuid(), Login = Guid.NewGuid().ToString(),
            FullName = name, DepartmentId = _department, Role = UserRole.Employee, IsActive = true };
        await _users.SaveAsync(user, _ct);
        return user;
    }

    private async Task<WorkDay> AddWorkDay(Guid employeeId, WorkDayStatus status = WorkDayStatus.Active)
    {
        var day = new WorkDay { Id = Guid.NewGuid(), UserId = employeeId, Date = _date,
            StartTime = TimeSpan.FromHours(9), Status = status, CountedInReports = true,
            EndTime = status == WorkDayStatus.Active ? null : TimeSpan.FromHours(17) };
        await _days.SaveAsync(day, _ct);
        return day;
    }

    [Fact]
    public async Task WithoutWorkDay_ReturnsNotStarted()
    {
        await AddUser();
        var row = Assert.Single(await Service().GetSummaryAsync(_date, _department, _ct));
        Assert.Equal("notStarted", row.Status);
        Assert.Equal(0, row.WorkedMinutes);
    }

    [Fact]
    public async Task OpenShiftAndBreak_ReturnsOnBreak()
    {
        var user = await AddUser();
        var day = await AddWorkDay(user.Id);
        await _breaks.SaveAsync(new Break { Id = Guid.NewGuid(), WorkDayId = day.Id,
            StartTime = TimeSpan.FromHours(10), EndTime = null }, _ct);
        var row = Assert.Single(await Service().GetSummaryAsync(_date, _department, _ct));
        Assert.Equal("onBreak", row.Status);
        Assert.Equal(1, row.BreakCount);
        Assert.True(row.WorkedMinutes >= 0);
    }

    [Fact]
    public async Task FinishedShift_ExcludesShortBreakFromWorkedTime()
    {
        var user = await AddUser();
        var day = await AddWorkDay(user.Id, WorkDayStatus.Finished);
        await _breaks.SaveAsync(new Break { Id = Guid.NewGuid(), WorkDayId = day.Id,
            StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10.5),
            Duration = TimeSpan.FromMinutes(30), CountedInReports = true }, _ct);
        await _breaks.SaveAsync(new Break { Id = Guid.NewGuid(), WorkDayId = day.Id,
            StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(11) + TimeSpan.FromMinutes(2),
            Duration = TimeSpan.FromMinutes(2), CountedInReports = false }, _ct);
        var row = Assert.Single(await Service().GetSummaryAsync(_date, _department, _ct));
        Assert.Equal("finished", row.Status);
        Assert.Equal(450, row.WorkedMinutes);
        Assert.Equal(2, row.BreakCount);
        Assert.Equal(32, row.BreakMinutes);
    }

    [Fact]
    public async Task AbsenceOverridesMissingWorkDay()
    {
        var user = await AddUser();
        await _absences.SaveAsync(new Absence { Id = Guid.NewGuid(), UserId = user.Id,
            Date = _date, Reason = AbsenceReason.Leave }, _ct);
        var row = Assert.Single(await Service().GetSummaryAsync(_date, _department, _ct));
        Assert.Equal("absent", row.Status);
        Assert.Equal("Leave", row.AbsenceReason);
    }

    [Fact]
    public async Task DepartmentFilter_DoesNotLeakOtherDepartments()
    {
        await AddUser();
        await _users.SaveAsync(new User { Id = Guid.NewGuid(), Login = "other", FullName = "Другой отдел",
            Role = UserRole.Employee, DepartmentId = Guid.NewGuid(), IsActive = true }, _ct);
        Assert.Single(await Service().GetSummaryAsync(_date, _department, _ct));
    }
}
