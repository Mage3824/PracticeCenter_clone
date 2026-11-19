// Endpoint 11 of api-contracts.md: an administrator or manager sets an "Absence" status for an
// employee (BR25 reason, BR28 one record per day reusing its id, BR27 the tooltip that names who set
// it). The author is the token subject; only the target employee and the date/reason travel in the
// request. BR25/BR28 role gates inside the service arrive as 409, a missing actor or target as 404.
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using WorkTime.Domain;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api;

/// <summary>Статусы отсутствия сотрудников. Управленческие пути закрыты политикой на методах, сотрудник читает только собственный статус (BR27).</summary>
[ApiController]
[Route("api/absences")]
// Все аутентифицированные роли: управленческие пути закрыты политикой на методах,
// сотрудник читает только собственный статус (BR27).
[Authorize]
public class AbsencesController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly AbsenceService _absenceService;

    private readonly UserService _userService;

    private readonly RequestContextProvider _requestContext;

    /// <summary>Контроллер отсутствий: сервис статусов, проверка личности субъекта и контекст запроса.</summary>
    public AbsencesController(
        AbsenceService absenceService,
        UserService userService,
        RequestContextProvider requestContext)
    {
        _absenceService = absenceService;
        _userService = userService;
        _requestContext = requestContext;
    }

    /// <summary>Ставит или меняет статус отсутствия сотрудника на один календарный день (BR25 причина, BR28 одна запись за день переиспользует свой id). Автор — субъект токена, в теле только дата и причина. 200 с AbsenceWire, 400 на нечитаемую дату или причину, 404 на неизвестного актора или сотрудника, 409 на ролевой гейт BR25 и BR28.</summary>
    [HttpPost("{employeeId}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AbsenceWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AbsenceWire> TrySetAbsence(Guid employeeId, SetAbsenceRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var date = RequestParsers.TryParseDateText(payload.Date, "date");
        var reason = RequestParsers.TryParseAbsenceReason(payload.Reason);

        // BR28: setting a status for a day that already has one reuses the existing record's id.
        var absence = await _absenceService.SetAbsenceAsync(actorId, employeeId, date, reason, cancellationToken);
        return WireConversion.TryFromAbsence(absence);
    }

    // PUT — та же семантика, что и POST: BR28 переиспользует id уже существующей записи за день.
    /// <summary>Та же семантика, что и POST: BR28 переиспользует id уже существующей записи за день. 200 с AbsenceWire, 400 на нечитаемую дату или причину, 404 на неизвестного актора или сотрудника, 409 на ролевой гейт BR25 и BR28.</summary>
    [HttpPut("{employeeId}")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AbsenceWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AbsenceWire> TryUpdateAbsence(Guid employeeId, SetAbsenceRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var date = RequestParsers.TryParseDateText(payload.Date, "date");
        var reason = RequestParsers.TryParseAbsenceReason(payload.Reason);

        var absence = await _absenceService.SetAbsenceAsync(actorId, employeeId, date, reason, cancellationToken);
        return WireConversion.TryFromAbsence(absence);
    }

    // Чтение отсутствия за день (BR27 tooltip: кто поставил статус — createdByUserId).
    /// <summary>Статус отсутствия за день (BR27: tooltip показывает, кто поставил статус). Сотрудник читает только свой id, чужой id — 403. 200 со списком AbsenceWire, 400 на нечитаемую дату.</summary>
    [HttpGet("{employeeId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AbsenceWire[]))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TryGetAbsencesForDate(Guid employeeId, [FromQuery] string? date)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var day = RequestParsers.TryParseDateText(date, "date");

        // BR27: сотрудник читает статус по своему employeeId; чужой id -> 403.
        var subjectId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        if (!User.IsInRole("Manager") && !User.IsInRole("Admin") && employeeId != subjectId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var found = await _absenceService.GetAbsencesForEmployeeOnDateAsync(employeeId, day, cancellationToken);
        return Ok(TryFromAbsenceEntities(found));
    }

    /// <summary>Отсутствия сотрудников в диапазоне дат — путь руководителя и администратора. 200 со списком AbsenceWire (пустой список, когда диапазон пуст), 400 на нечитаемые границы диапазона.</summary>
    [HttpGet("")]
    [Authorize(Policy = "ManagerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AbsenceWire[]))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<AbsenceWire[]> TryGetAbsencesInRange([FromQuery] string? from, [FromQuery] string? to)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var start = RequestParsers.TryParseDateText(from, "from");
        var end = RequestParsers.TryParseDateText(to, "to");

        var found = await _absenceService.GetAbsencesInRangeAsync(start, end, cancellationToken);
        return TryFromAbsenceEntities(found);
    }

    private static AbsenceWire[] TryFromAbsenceEntities(IEnumerable<Absence> found)
    {
        var result = new List<AbsenceWire>();

        foreach (var absence in found)
        {
            result.Add(WireConversion.TryFromAbsence(absence));
        }

        return result.ToArray();
    }
}

