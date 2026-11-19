// Service that manages breaks inside an open working day.
// Rules covered: BR6 (break only during an active work day), BR8 (a break shorter
// than 5 minutes is not counted and not saved in reports - no exception), BR9
// (total break time per day is capped at 4 hours - the system does NOT reject such
// a break, it marks WorkDay.IsBreakLimitExceeded so the manager is notified), BR10
// (only one open break at a time), BR11 (break time is subtracted from worked time,
// never added).
//
// The service resolves the breaks of a day itself through
// IBreakRepository.GetByWorkDayIdAsync, so no method here receives a list of break
// ids from its caller.
using WorkTime.Domain;

/// <summary>
/// Breaks inside an open working day: only during an active day (BR6), at most one open
/// break (BR10), short breaks excluded rather than rejected (BR8), and the 4 hour cap
/// recorded as a flag instead of an error (BR9).
/// </summary>
public class BreakService
{
    private readonly IBreakRepository _breaks;

    // Proves that the day a break belongs to is still open (BR6) and resolves the
    // owning employee of a break.
    private readonly IWorkDayRepository _workDays;

    /// <summary>
    /// Builds the service over the break store and the day store that proves a day is open.
    /// </summary>
    /// <param name="breaks">Break store.</param>
    /// <param name="workDays">Day store: owning day of a break and its open state (BR6, BR21).</param>
    public BreakService(IBreakRepository breaks, IWorkDayRepository workDays)
    {
        _breaks = breaks;
        _workDays = workDays;
    }

    /// <summary>
    /// Opens a break inside an active working day (BR6, BR9, BR10).
    /// </summary>
    /// <param name="employeeId">Owner of the day the break belongs to.</param>
    /// <param name="workDayId">Day the break is opened inside.</param>
    /// <param name="startTime">Time of day the break starts.</param>
    /// <param name="cancellationToken">Cancels the storage calls.</param>
    /// <returns>The open break record.</returns>
    /// <remarks>A closed day throws BusinessRuleException BR6; a second open break throws BR10. Passing the 4 hour cap only flags the day (BR9).</remarks>
    public async Task<Break> StartBreakAsync(
        Guid employeeId,
        Guid workDayId,
        TimeSpan startTime,
        CancellationToken cancellationToken)
    {
        var workDay = await _workDays.GetByIdAsync(workDayId, cancellationToken);
        if (workDay == null)
        {
            throw new EntityNotFoundException(workDayId, "WorkDay");
        }

        if (workDay.UserId != employeeId)
        {
            throw new BusinessRuleException("BR21", "[RU] This work day belongs to another employee.");
        }

        // BR6: a break can only be started between "Start work" and "Finish work".
        if (workDay.Status != WorkDayStatus.Active || workDay.EndTime != null)
        {
            throw new BusinessRuleException(
                "BR6",
                "[RU] A break can be started only during an open working day.");
        }

        // BR10: an employee can have only one active break at a time.
        var openBreaks = await _breaks.GetUnfinishedForEmployeeAsync(employeeId, cancellationToken);
        if (openBreaks.Count > 0)
        {
            throw new BusinessRuleException(
                "BR10",
                "[RU] Another break is still open - finish it before starting a new one.");
        }

        // BR9: passing the 4 hour cap is not an error. The day is flagged so that the
        // notification for the manager can be produced from the flag; opening the
        // break continues normally.
        var alreadyTaken = await SumCountedBreakTimeAsync(workDayId, Guid.Empty, cancellationToken);
        if (alreadyTaken >= WorkTimeRules.MaxBreakDuration)
        {
            workDay.IsBreakLimitExceeded = true;
            await _workDays.SaveAsync(workDay, cancellationToken);
        }

        var workBreak = new Break
        {
            Id = Guid.NewGuid(),
            WorkDayId = workDayId,
            StartTime = startTime,
            EndTime = null,
            Duration = null,
            CountedInReports = true,
        };

        await _breaks.SaveAsync(workBreak, cancellationToken);
        return workBreak;
    }

    /// <summary>
    /// Closes an open break and records whether it counts toward worked time (BR6, BR8, BR9, BR21).
    /// </summary>
    /// <param name="breakId">Break to close.</param>
    /// <param name="endTime">Time of day the break ends.</param>
    /// <param name="employeeId">Employee performing the action - must own the day (BR21).</param>
    /// <param name="cancellationToken">Cancels the storage calls.</param>
    /// <returns>The finished break; a break under 5 minutes is stored as not counted (BR8).</returns>
    /// <remarks>Someone else's break throws BusinessRuleException BR21; a closed day throws BR6. The 4 hour cap flags the day, never rejects (BR9).</remarks>
    public async Task<Break> FinishBreakAsync(
        Guid breakId,
        TimeSpan endTime,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var workBreak = await _breaks.GetByIdAsync(breakId, cancellationToken);
        if (workBreak == null)
        {
            throw new EntityNotFoundException(breakId, "Break");
        }

        if (workBreak.EndTime != null)
        {
            throw new BusinessRuleException(
                "BR10",
                "[RU] This break is already finished.");
        }

        var workDay = await _workDays.GetByIdAsync(workBreak.WorkDayId, cancellationToken);
        if (workDay == null)
        {
            throw new EntityNotFoundException(workBreak.WorkDayId, "WorkDay");
        }

        // BR21 (правка аудита 4.1): перерыв завершает только владелец рабочего дня. Чужой
        // перерыв — бизнес-ошибка BR21 (409 в глобальном обработчике), а не silently чужая правка.
        if (workDay.UserId != employeeId)
        {
            throw new BusinessRuleException(
                "BR21",
                "[RU] Этот перерыв принадлежит чужому рабочему дню (BR21).");
        }

        // BR6: finishing is only meaningful while the owning day is still open.
        if (workDay.Status != WorkDayStatus.Active || workDay.EndTime != null)
        {
            throw new BusinessRuleException(
                "BR6",
                "[RU] This break belongs to a working day that is not open.");
        }

        var duration = endTime - workBreak.StartTime;
        if (duration < TimeSpan.Zero)
        {
            throw new BusinessRuleException(
                "BR8",
                "[RU] Break end time is earlier than its start time.");
        }

        // Правка аудита 3.2/3.3: мьютекс убран, путь вызова полностью асинхронный.
        // BR9: the limit is recorded, never enforced by rejection. The break finishes
        // and the day carries the flag that turns into the manager notification.
        var otherBreaks = await SumCountedBreakTimeAsync(workBreak.WorkDayId, workBreak.Id, cancellationToken);
        if (otherBreaks + duration > WorkTimeRules.MaxBreakDuration)
        {
            workDay.IsBreakLimitExceeded = true;
            await _workDays.SaveAsync(workDay, cancellationToken);
        }

        workBreak.EndTime = endTime;
        workBreak.Duration = duration;

        // BR8: shorter than 5 minutes -> not counted in worked time and not saved in
        // reports. This is NOT an error, the break is simply excluded.
        workBreak.CountedInReports = duration >= WorkTimeRules.MinBreakDuration;

        await _breaks.SaveAsync(workBreak, cancellationToken);
        return workBreak;
    }

    // Sum of counted, already-finished break durations of one work day. The break
    // whose id equals excludeBreakId is skipped (it is being finished by the caller);
    // pass Guid.Empty to include every break of the day.
    private async Task<TimeSpan> SumCountedBreakTimeAsync(
        Guid workDayId,
        Guid excludeBreakId,
        CancellationToken cancellationToken)
    {
        var dayBreaks = await _breaks.GetByWorkDayIdAsync(workDayId, cancellationToken);

        var total = TimeSpan.Zero;
        foreach (var workBreak in dayBreaks)
        {
            if (workBreak.Id == excludeBreakId)
            {
                continue;
            }

            if (!workBreak.CountedInReports || workBreak.EndTime == null)
            {
                continue;
            }

            var duration = workBreak.Duration ?? (workBreak.EndTime.Value - workBreak.StartTime);
            total += duration;
        }

        return total;
    }
}
