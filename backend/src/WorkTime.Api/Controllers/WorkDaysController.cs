// Endpoints 2 and 3 of api-contracts.md plus the "current shift" read the frontend needs to reopen
// its work day after a reload.
//
// The employee is always the token subject; date / startTime / endTime travel as contract scalars.
// Business-rule rejections (BR1 second open day, BR2 day not open, BR21 foreign day) are thrown by
// the services and turned into 409/404 envelopes by WorkTimeExceptionHandler.
using Microsoft.AspNetCore.Authorization;
using WorkTime.Domain;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api;

/// <summary>Рабочие дни: открытие и закрытие смены, текущая открытая смена, аварийное авто-завершение и состояние кнопки «Начать работу». Все аутентифицированные роли (BR19, BR3), владение днём проверяет сервис (BR21 — 409).</summary>
[ApiController]
[Route("api/work-days")]
// Все аутентифицированные роли (BR19, BR3): владение днём проверяет сервис (BR21 -> 409).
[Authorize]
public class WorkDaysController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly WorkTimeService _workTimeService;

    private readonly UserService _userService;

    private readonly AbsenceService _absenceService;

    private readonly RequestContextProvider _requestContext;

    /// <summary>Контроллер рабочих дней: сервис смен, сервис отсутствий и контекст запроса.</summary>
    public WorkDaysController(
        WorkTimeService workTimeService,
        UserService userService,
        AbsenceService absenceService,
        RequestContextProvider requestContext)
    {
        _workTimeService = workTimeService;
        _userService = userService;
        _absenceService = absenceService;
        _requestContext = requestContext;
    }

    /// <summary>Открывает рабочий день. 200 с WorkDayWire, 400 на нечитаемые дату или время, 404 на неизвестного субъекта токена, 409 на BR1 (день уже открыт) и BR27 (день помечен отсутствием).</summary>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkDayWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<WorkDayWire> TryStartWorkDay(StartWorkDayRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        // Stage-2 identity gap: the service does not resolve the user, so an unknown subject would
        // otherwise create a day for nobody.
        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, cancellationToken);

        var date = RequestParsers.TryParseDateText(payload.Date, "date");
        var startTime = RequestParsers.TryParseTimeOfDayText(payload.StartTime, "startTime");

        var workDay = await _workTimeService.StartWorkDayAsync(employeeId, date, startTime, cancellationToken);
        return WireConversion.TryFromWorkDay(workDay);
    }

    /// <summary>Закрывает рабочий день; открытый перерыв закрывается автоматически вместе с днём (BR7 делает сервис). 200 с WorkDayWire, 400 на нечитаемое время, 404 на отсутствующий день, 409 на BR2 (день не открыт) и BR21 (чужой день).</summary>
    [HttpPost("{workDayId}/finish")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkDayWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<WorkDayWire> TryFinishWorkDay(Guid workDayId, FinishWorkDayRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, cancellationToken);

        var endTime = RequestParsers.TryParseTimeOfDayText(payload.EndTime, "endTime");

        // BR7 side effect (an open break is auto-closed with the day) lives in the service.
        var workDay = await _workTimeService.FinishWorkDayAsync(employeeId, workDayId, endTime, cancellationToken);
        return WireConversion.TryFromWorkDay(workDay);
    }

    /// <summary>Текущая открытая смена сотрудника. Открытой смены нет — 200 с телом null, а не 204: контракт различает «нечего показать» и «ошибка произошла». 400 на нечитаемого субъекта.</summary>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkDayWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TryGetCurrentWorkDay([FromQuery] DateTime? date)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, cancellationToken);

        // "No open shift" is not a 404: the contract answers with a null body (200), because the
        // frontend distinguishes "nothing to show" from "an error happened".
        var openDay = date.HasValue
            ? await _workTimeService.GetOpenWorkDayOnDateAsync(employeeId, date.Value, cancellationToken)
            : await _workTimeService.GetOpenWorkDayAsync(employeeId, cancellationToken);

        if (openDay != null)
        {
            return Ok(WireConversion.TryFromWorkDay(openDay));
        }

        // Значение null в MVC даёт 204 No Content, а §2 требует 200. Открытой смены нет ->
        // 200 OK с телом literal "null" и content-type application/json (как в ReportsController):
        // контракт §2 объявляет WorkDayWire или null, пустое тело контракту противоречит.
        return new ContentResult
        {
            ContentType = "application/json",
            StatusCode = StatusCodes.Status200OK,
            Content = "null",
        };
    }

    /// <summary>History of work days for current authenticated user.</summary>
    [HttpGet("history")]
    public async Task<WorkDayWire[]> GetHistory([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (to.Date < from.Date || (to.Date - from.Date).TotalDays > 366)
            throw new ArgumentException("Date range must be between 0 and 366 days.");
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, _requestContext.TryStopSignal());
        var days = await _workTimeService.GetHistoryAsync(employeeId, from, to, _requestContext.TryStopSignal());
        return days.OrderByDescending(d => d.Date).Select(WireConversion.TryFromWorkDay).ToArray();
    }

    // BR3: аварийное авто-завершение смены на 23:59:59 (запись в журнал делает сервис).
    /// <summary>Аварийное авто-завершение смены на 23:59:59 (BR3): запись в журнал событий делает сервис. 200 с WorkDayWire, 404 на отсутствующий день, 409 на BR2 (день не открыт).</summary>
    [HttpPost("{workDayId}/auto-finish")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkDayWire))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<WorkDayWire> TryAutoFinishWorkDay(Guid workDayId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var employeeId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, employeeId, cancellationToken);

        var workDay = await _workTimeService.AutoFinishWorkDayAsync(employeeId, workDayId, cancellationToken);
        return WireConversion.TryFromWorkDay(workDay);
    }

    // BR27: состояние кнопки «Начать работу». BR21 — сотрудник спрашивает только про себя.
    /// <summary>Состояние кнопки «Начать работу»: BR27 приходит как false, тот же факт при открытии дня выбрасывает 409. 200 с CanStartWorkDayWire, 400 на нечитаемую дату, 409 на BR21 (вопрос про чужого сотрудника).</summary>
    [HttpGet("{employeeId}/can-start")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CanStartWorkDayWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<CanStartWorkDayWire> TryCanStartWorkDay(Guid employeeId, [FromQuery] string? date)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var callerId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, callerId, cancellationToken);

        if (employeeId != callerId)
        {
            throw new BusinessRuleException("BR21", "[RU] An employee may check only their own work day.");
        }

        var day = RequestParsers.TryParseDateText(date, "date");
        var canStart = await _absenceService.CanStartWorkDayAsync(employeeId, day, cancellationToken);

        return new CanStartWorkDayWire { CanStart = canStart };
    }
}

