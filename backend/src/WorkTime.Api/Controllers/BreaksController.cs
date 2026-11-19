// Endpoints 5 and 6 of api-contracts.md: the breaks of a working day.
//
// The break start is addressed by the working day it belongs to (BR6), so its route keeps the
// work-day prefix even though the entity is a Break; finishing one only needs the break Id, which
// resolves its owner inside the service through Break.WorkDayId.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkTime.Domain;

namespace WorkTime.Api;

/// <summary>Перерывы рабочего дня: начало адресуется днём, которому принадлежит перерыв (BR6), закрытие — только id перерыва. Все аутентифицированные роли (BR19), владение днём проверяет сервис (BR21 — 409).</summary>
[ApiController]
[Route("api")]
// Все аутентифицированные роли (BR19): владение днём проверяет сервис (BR21 -> 409).
[Authorize]
public class BreaksController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly BreakService _breakService;

    private readonly UserService _userService;

    private readonly RequestContextProvider _requestContext;
    private readonly IWorkDayRepository _workDays;
    private readonly IBreakRepository _breaks;

    /// <summary>Контроллер перерывов: сервис перерывов и проверка личности субъекта.</summary>
    public BreaksController(
        BreakService breakService,
        UserService userService,
        RequestContextProvider requestContext,
        IWorkDayRepository workDays,
        IBreakRepository breaks)
    {
        _breakService = breakService;
        _userService = userService;
        _requestContext = requestContext;
        _workDays = workDays;
        _breaks = breaks;
    }

    /// <summary>List breaks of a work day owned by the authenticated user.</summary>
    [HttpGet("work-days/{workDayId}/breaks")]
    public async Task<BreakWire[]> GetBreaks(Guid workDayId)
    {
        var ct = _requestContext.TryStopSignal();
        var caller = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        var day = await _workDays.GetByIdAsync(workDayId, ct)
            ?? throw new EntityNotFoundException(workDayId, "WorkDay");
        if (day.UserId != caller)
            throw new BusinessRuleException("BR21", "This work day belongs to another employee.");
        var result = await _breaks.GetByWorkDayIdAsync(workDayId, ct);
        return result.OrderBy(b => b.StartTime).Select(WireConversion.TryFromBreak).ToArray();
    }

    /// <summary>Начинает перерыв открытого рабочего дня. 200 с BreakWire, 400 на нечитаемое время, 404 на отсутствующий день, 409 на BR6 (день не открыт), BR10 (перерыв уже открыт) и BR21 (чужой день). BR9 не является ошибкой: превышение лимита перерывов ставит флаг isBreakLimitExceeded.</summary>
    [HttpPost("work-days/{workDayId}/breaks/start")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BreakWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<BreakWire> TryStartBreak(Guid workDayId, StartBreakRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, cancellationToken);

        var startTime = RequestParsers.TryParseTimeOfDayText(payload.StartTime, "startTime");

        // BR9 is not an error here: exceeding the 4 hour break cap sets isBreakLimitExceeded on the
        // day instead of rejecting the request.
        var workBreak = await _breakService.StartBreakAsync(employeeId, workDayId, startTime, cancellationToken);
        return WireConversion.TryFromBreak(workBreak);
    }

    /// <summary>Закрывает перерыв. 200 с BreakWire, 400 на нечитаемое время, 404 на отсутствующий перерыв или день, 409 на BR6, BR8 (конец раньше начала) и BR10 (уже закрыт). Перерыв короче пяти минут закрывается без ошибки и не считается в отчётах (BR8).</summary>
    [HttpPost("breaks/{breakId}/finish")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BreakWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<BreakWire> TryFinishBreak(Guid breakId, FinishBreakRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, cancellationToken);

        var endTime = RequestParsers.TryParseTimeOfDayText(payload.EndTime, "endTime");

        // A break shorter than 5 minutes is silent (BR8): it finishes normally and comes back with
        // countedInReports = false.
        var workBreak = await _breakService.FinishBreakAsync(breakId, endTime, employeeId, cancellationToken);
        return WireConversion.TryFromBreak(workBreak);
    }
}

