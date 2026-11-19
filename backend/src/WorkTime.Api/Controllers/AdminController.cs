// Endpoint 15 of api-contracts.md (the path this Stage gives it: GET /api/audit-logs). BR29 makes the
// event log readable by the administrator only, which is exactly what the AdminOnly policy enforces;
// BR30 needs the previous value, the author and the time, and AuditLogDto already carries all three.
//
// Конструктор держит только сервисы Application-слоя: журнал событий читается через AuditLogService,
// �������� � �������� ���������� ������� (BR23.1/BR23.2) � ����� PeriodClosureService. �����������
// здесь не инжектится: правила и записи журнала живут в Application-слое, на контроллере остаются
// wire-маппинг, разбор query-параметра и личность субъекта (RequestIdentity через UserService).
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api
{
    /// <summary>Административные чтения и операции над расчётными периодами: доступ только администратору (BR29, BR23.1, BR23.2).</summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private readonly AuditLogService _auditLogs;

        private readonly PeriodClosureService _periodClosures;

        private readonly UserService _userService;

        private readonly RequestContextProvider _requestContext;

        /// <summary>Контроллер администратора: журнал событий и сервис расчётных периодов.</summary>
        public AdminController(
            AuditLogService auditLogs,
            PeriodClosureService periodClosures,
            UserService userService,
            RequestContextProvider requestContext)
        {
            _auditLogs = auditLogs;
            _periodClosures = periodClosures;
            _userService = userService;
            _requestContext = requestContext;
        }

        // entityId is required: the log is read per entity (GetEntityHistoryAsync), and the client sends
        // that criterion rather than a list of entries.
        /// <summary>История событий одной сущности: клиент передаёт критерий entityId, а не список записей (BR30 несёт предыдущее значение, автора и время). 200 со списком AuditLogWire, 400 на нечитаемый id.</summary>
        [HttpGet("/api/audit-logs")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuditLogWire[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<AuditLogWire[]> TryGetAuditHistory(Guid entityId)
        {
            var cancellationToken = _requestContext.TryStopSignal();

            if (entityId == Guid.Empty)
            {
                throw new ArgumentException(
                    "[RU] Query parameter 'entityId' is required.",
                    paramName: "ENTITY_ID_MISSING");
            }

            var history = await _auditLogs.GetEntityHistoryAsync(entityId, cancellationToken);
            return WireConversion.TryFromAuditLogs(history);
        }

        // BR23.1: закрытие расчётного периода настраивается администратором; с этого момента
        // редактирование данных внутри периода блокируется (BR12 в сервисе отчётов).
        /// <summary>Закрывает расчётный период (BR23.1): с этого момента редактирование данных внутри периода блокируется. 200 с PeriodClosureWire, 404 на неизвестный id, 409 на уже закрытый период.</summary>
        [HttpPost("/api/period-closures/{periodClosureId}/close")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PeriodClosureWire))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<PeriodClosureWire> TryClosePeriod(Guid periodClosureId)
        {
            var cancellationToken = _requestContext.TryStopSignal();
            var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

            await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

            var closure = await _periodClosures.ClosePeriodAsync(periodClosureId, actorId, cancellationToken);
            return ManagementWireConversion.TryFromPeriodClosure(closure);
        }

        // BR23.2: вскрытие закрытого периода требует причину / номер акта и логируется как
        // ����������� �������������. ������ ������� ������ ������ � ��� �� ������� ��������,
        // что был в контроллере (личность актора, затем 400 на пустой причины, затем 404 на id).
        /// <summary>Вскрывает закрытый расчётный период (BR23.2): причина или номер акта обязательны, операция логируется как экстренная корректировка. 200 с PeriodClosureWire, 400 на пустую причину, 404 на неизвестный id, 409 на незакрытый период.</summary>
        [HttpPost("/api/period-closures/{periodClosureId}/reopen")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PeriodClosureWire))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<PeriodClosureWire> TryReopenPeriod(Guid periodClosureId, ReopenPeriodRequest payload)
        {
            var cancellationToken = _requestContext.TryStopSignal();
            var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());

            await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

            var closure = await _periodClosures.ReopenPeriodAsync(
                periodClosureId, payload.Reason, actorId, cancellationToken);
            return ManagementWireConversion.TryFromPeriodClosure(closure);
        }
    }
}

