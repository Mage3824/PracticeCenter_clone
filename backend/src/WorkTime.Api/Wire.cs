// Ответные («проводные») модели WorkTime.Api и решения по форматированию скаляров (этап 2).
//
// Имена свойств в C# — PascalCase, а ключ JSON задаётся явно атрибутом [JsonPropertyName], поэтому
// контракт api-contracts.md на проводе не меняется. Каждое поле уже несёт формат контракта, когда
// его заполняет конвертер ниже: Guid идёт как канонический UUID-текст, дата — "YYYY-MM-DD",
// время суток — "HH:mm", длительности — целые минуты (суффикс Minutes в ключах), timestamps —
// "YYYY-MM-DDTHH:mm:ss" без суффикса часового пояса, перечисления — имя константы C#.
//
// null в поле означает «не применимо сейчас» (у открытой смены нет endTime, у неудачного логина
// нет accessToken); фронт трактует null как «отсутствует».
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using WorkTime.Domain;

namespace WorkTime.Api;

/// <summary>Ответ эндпоинта входа: флаги результата (BR35 блокировка, BR36 CAPTCHA) и только при успехе — выданный токен.</summary>
public class LoginWire
{
    /// <summary>Логин прошёл успешно.</summary>
    [JsonPropertyName("isSuccess")] public bool IsSuccess { get; set; } = false;

    /// <summary>Нужна CAPTCHA (BR36).</summary>
    [JsonPropertyName("requiresCaptcha")] public bool RequiresCaptcha { get; set; } = false;

    /// <summary>Вход блокирован (BR35).</summary>
    [JsonPropertyName("isLocked")] public bool IsLocked { get; set; } = false;

    /// <summary>Текст подсказки для экрана входа.</summary>
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;

    /// <summary>Момент, до которого вход закрыт; null когда блокировки нет.</summary>
    [JsonPropertyName("lockedUntil")] public string? LockedUntil { get; set; }

    // Stage-2 addition: present only when the login succeeded. api-contracts.md §7.3 keeps the
    // failure body free of these keys, so a null value is not written out at all.
    /// <summary>Выданный JWT; ключ отсутствует в ответе при неуспешном входе.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }

    /// <summary>Срок жизни токена в секундах; отсутствует при неуспешном входе.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("expiresIn")] public long? ExpiresIn { get; set; }
}

/// <summary>Рабочий день на проводе: дата «YYYY-MM-DD», время «HH:mm».</summary>
public class WorkDayWire
{
    /// <summary>Идентификатор дня.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>Сотрудник, которому принадлежит день.</summary>
    [JsonPropertyName("userId")] public Guid UserId { get; set; }

    /// <summary>Календарная дата дня — серверные границы рабочего дня владеет сервер.</summary>
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;

    /// <summary>Время открытия смены.</summary>
    [JsonPropertyName("startTime")] public string StartTime { get; set; } = string.Empty;

    /// <summary>Время закрытия смены; null когда день ещё открыт (BR2).</summary>
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }

    /// <summary>Токен статуса из WorkDayStatus: Active, Finished, AutoFinished.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;

    /// <summary>День учитывается в отчётах (BR5).</summary>
    [JsonPropertyName("countedInReports")] public bool CountedInReports { get; set; } = true;

    /// <summary>Лимит перерывов превышен (BR9) — это флаг, а не ошибка.</summary>
    [JsonPropertyName("isBreakLimitExceeded")] public bool IsBreakLimitExceeded { get; set; } = false;
}

/// <summary>Перерыв рабочего дня на проводе.</summary>
public class BreakWire
{
    /// <summary>Идентификатор перерыва.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>День, которому принадлежит перерыв.</summary>
    [JsonPropertyName("workDayId")] public Guid WorkDayId { get; set; }

    /// <summary>Время начала перерыва.</summary>
    [JsonPropertyName("startTime")] public string StartTime { get; set; } = string.Empty;

    /// <summary>Время закрытия перерыва; null когда перерыв ещё открыт (BR10).</summary>
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }

    /// <summary>Длительность перерыва в целых минутах; null когда перерыв ещё открыт.</summary>
    [JsonPropertyName("durationMinutes")] public long? DurationMinutes { get; set; }

    /// <summary>Перерыв учитывается в отчётах; false для перерыва короче пяти минут (BR8).</summary>
    [JsonPropertyName("countedInReports")] public bool CountedInReports { get; set; } = true;
}

/// <summary>Текст отчёта дня на проводе.</summary>
public class ReportWire
{
    /// <summary>Идентификатор отчёта.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>День, о котором написан отчёт.</summary>
    [JsonPropertyName("workDayId")] public Guid WorkDayId { get; set; }

    /// <summary>Текст отчёта.</summary>
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

    /// <summary>Момент создания отчёта — серверное время без суффикса часовой зоны.</summary>
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Момент последней правки; null пока отчёт не менялся.</summary>
    [JsonPropertyName("updatedAt")] public string? UpdatedAt { get; set; }
}

/// <summary>Дневный свод закрытого дня: границы смены, перерывы и итог.</summary>
public class DailyReportWire
{
    /// <summary>Время открытия смены.</summary>
    [JsonPropertyName("startTime")] public string StartTime { get; set; } = string.Empty;

    /// <summary>Время закрытия смены; null когда день ещё открыт.</summary>
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }

    /// <summary>Перерывы внутри дня; пустой список когда перерывов нет.</summary>
    [JsonPropertyName("breaks")] public List<ReportBreakWire> Breaks { get; set; } = new();

    /// <summary>Итог дня в целых минутах.</summary>
    [JsonPropertyName("totalMinutes")] public long TotalMinutes { get; set; } = 0;
}

/// <summary>Перерыв внутри дневного свода.</summary>
public class ReportBreakWire
{
    /// <summary>Время начала перерыва.</summary>
    [JsonPropertyName("startTime")] public string StartTime { get; set; } = string.Empty;

    /// <summary>Время закрытия перерыва; null когда перерыв ещё открыт.</summary>
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }
}

/// <summary>Месячная строка сводки по сотруднику.</summary>
public class MonthlyReportWire
{
    /// <summary>Отображаемое имя сотрудника.</summary>
    [JsonPropertyName("employeeName")] public string EmployeeName { get; set; } = string.Empty;

    // Sparse: keyed by "YYYY-MM-DD", value = integer minutes (BR15/BR26/BR5 skip days).
    /// <summary>Часы по дням: ключ «YYYY-MM-DD», значение в целых минутах; дни, которые BR15, BR26 и BR5 пропускают, остаются отсутствующими.</summary>
    [JsonPropertyName("hoursByDay")] public Dictionary<string, long> HoursByDay { get; set; } = new();

    /// <summary>Итог месяца в целых минутах.</summary>
    [JsonPropertyName("totalMinutes")] public long TotalMinutes { get; set; } = 0;

    /// <summary>Дни, помеченные отсутствием; пустой список когда таких дней нет.</summary>
    [JsonPropertyName("absenceDays")] public List<string> AbsenceDays { get; set; } = new();
}

/// <summary>Статус отсутствия за день на проводе.</summary>
public class AbsenceWire
{
    /// <summary>Идентификатор записи статуса.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>Сотрудник, которому поставлен статус.</summary>
    [JsonPropertyName("userId")] public Guid UserId { get; set; }

    /// <summary>Календарная дата статуса.</summary>
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;

    /// <summary>Токен причины из AbsenceReason: Sick, Leave, DayOff, Absence.</summary>
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;

    /// <summary>Текст Guid того, кто поставил статус (BR27 tooltip); null когда автор неизвестен.</summary>
    [JsonPropertyName("createdByUserId")] public string? CreatedByUserId { get; set; }
}

/// <summary>Запись журнала событий на проводе (BR29, BR30).</summary>
public class AuditLogWire
{
    /// <summary>Идентификатор записи журнала.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }

    /// <summary>Автор события.</summary>
    [JsonPropertyName("userId")] public Guid UserId { get; set; }

    /// <summary>Токен действия из AuditAction: Create, Update, Delete.</summary>
    [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;

    /// <summary>Тип изменённой сущности.</summary>
    [JsonPropertyName("entityType")] public string EntityType { get; set; } = string.Empty;

    /// <summary>Идентификатор изменённой сущности.</summary>
    [JsonPropertyName("entityId")] public Guid EntityId { get; set; }

    /// <summary>Значение до изменения (BR30); null для создания.</summary>
    [JsonPropertyName("oldValue")] public string? OldValue { get; set; }

    /// <summary>Значение после изменения (BR30); null для удаления.</summary>
    [JsonPropertyName("newValue")] public string? NewValue { get; set; }

    /// <summary>Момент события — серверное время без суффикса часовой зоны.</summary>
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = string.Empty;
}

// Scalar formatting decisions of §3 in api-contracts.md, kept in one place so every controller uses
// the same conversion. A nullable overload emits the "or null" half of a contract cell; the
// non-nullable one is used where the contract never allows null.
/// <summary>Одно место решений по скалярным типам: доменное значение становится текстом провода, и все контроллеры используют один и тот же конверт.</summary>
public class WireFormats
{
    private const string DateFormat = "yyyy-MM-dd";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss";

    // Date-only: midnight noise never crosses the wire (BR32/timezone policy is Stage 4).
    /// <summary>Дата «YYYY-MM-DD» без временной части: шум полуночи не пересекает провод.</summary>
    public static string TryDateText(DateTime date)
    {
        return date.Date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    // Server time, no timezone suffix.
    /// <summary>Серверное время без суффикса часовой зоны.</summary>
    public static string TryTimestampText(DateTime timestamp)
    {
        return timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    // Server time, no timezone suffix. Null stays null.
    /// <summary>Серверное время без суффикса часовой зоны; null остаётся null.</summary>
    public static string? TryTimestampText(DateTime? timestamp)
    {
        if (timestamp == null)
        {
            return null;
        }

        return timestamp.Value.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    // Time-of-day: 24 h, minutes only - seconds never matter for a clock time.
    /// <summary>Время суток «HH:mm», 24 часа, минуты — секунды для часов никогда не важны.</summary>
    public static string TryTimeOfDayText(TimeSpan timeOfDay)
    {
        return PadTwo(timeOfDay.Hours) + ":" + PadTwo(timeOfDay.Minutes);
    }

    // Time-of-day: 24 h, minutes only. Null stays null - null means "still open" (BR2/BR10/BR15).
    /// <summary>Время суток «HH:mm»; null означает «ещё открыто» (BR2, BR10, BR15).</summary>
    public static string? TryTimeOfDayText(TimeSpan? timeOfDay)
    {
        if (timeOfDay == null)
        {
            return null;
        }

        return TryTimeOfDayText(timeOfDay.Value);
    }

    // Duration: integer minutes. Null means "still open".
    /// <summary>Длительность как целые минуты; null означает «ещё открыто».</summary>
    public static long? TryMinutesOfDuration(TimeSpan? duration)
    {
        if (duration == null)
        {
            return null;
        }

        return TryMinutesOfSpan(duration.Value);
    }

    // Duration of a value that is always present (a report total). TimeSpan counts whole ticks, so
    // rounding only absorbs the floating-point error of TotalMinutes, never a real fraction.
    /// <summary>Длительность значения, которое всегда присутствует (итог отчёта); округление гасит только погрешность минütного представления.</summary>
    public static long TryMinutesOfSpan(TimeSpan duration)
    {
        return (long)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
    }

    // Guid as canonical UUID text, or null for an unset reference.
    /// <summary>Guid как канонический текст UUID; null для незаполненной ссылки.</summary>
    public static string? TryGuidText(Guid? id)
    {
        if (id == null || id.Value == Guid.Empty)
        {
            return null;
        }

        return id.Value.ToString("D", CultureInfo.InvariantCulture);
    }

    private static string PadTwo(int value)
    {
        return value.ToString("D2", CultureInfo.InvariantCulture);
    }
}

// DTO -> wire conversions. Nothing here knows a repository or a business rule: it only renames and
// formats what the Application layer already decided.
/// <summary>Перевод DTO и доменных сущностей в wire-формы: только переименование и форматирование того, что решил Application-слой.</summary>
public class WireConversion
{
    /// <summary>Результат входа в wire-форму; токен и срок добавляются только при успешном входе.</summary>
    public static LoginWire TryFromAuthResult(AuthResult result, string? accessToken, long expiresIn)
    {
        var wire = new LoginWire();
        wire.IsSuccess = result.IsSuccess;
        wire.RequiresCaptcha = result.RequiresCaptcha;
        wire.IsLocked = result.IsLocked;
        wire.Message = result.Message;
        wire.LockedUntil = WireFormats.TryTimestampText(result.LockedUntil);

        if (result.IsSuccess)
        {
            wire.AccessToken = accessToken;
            wire.ExpiresIn = expiresIn;
        }

        return wire;
    }

    /// <summary>DTO рабочего дня в wire-форму.</summary>
    public static WorkDayWire TryFromWorkDayDto(WorkDayDto dto)
    {
        var wire = new WorkDayWire();
        wire.Id = dto.Id;
        wire.UserId = dto.UserId;
        wire.Date = WireFormats.TryDateText(dto.Date);
        wire.StartTime = WireFormats.TryTimeOfDayText(dto.StartTime);
        wire.EndTime = WireFormats.TryTimeOfDayText(dto.EndTime);
        wire.Status = dto.Status.ToString();
        wire.CountedInReports = dto.CountedInReports;
        wire.IsBreakLimitExceeded = dto.IsBreakLimitExceeded;
        return wire;
    }

    // The services hand back domain entities, so the same fields are read straight off them - a DTO
    // exists for the frontend's view only.
    /// <summary>Доменный рабочий день в wire-форму: те же поля, DTO существует только для взгляда фронтенда.</summary>
    public static WorkDayWire TryFromWorkDay(WorkDay workDay)
    {
        var wire = new WorkDayWire();
        wire.Id = workDay.Id;
        wire.UserId = workDay.UserId;
        wire.Date = WireFormats.TryDateText(workDay.Date);
        wire.StartTime = WireFormats.TryTimeOfDayText(workDay.StartTime);
        wire.EndTime = WireFormats.TryTimeOfDayText(workDay.EndTime);
        wire.Status = workDay.Status.ToString();
        wire.CountedInReports = workDay.CountedInReports;
        wire.IsBreakLimitExceeded = workDay.IsBreakLimitExceeded;
        return wire;
    }

    /// <summary>Доменный перерыв в wire-форму.</summary>
    public static BreakWire TryFromBreak(Break workBreak)
    {
        var wire = new BreakWire();
        wire.Id = workBreak.Id;
        wire.WorkDayId = workBreak.WorkDayId;
        wire.StartTime = WireFormats.TryTimeOfDayText(workBreak.StartTime);
        wire.EndTime = WireFormats.TryTimeOfDayText(workBreak.EndTime);
        wire.DurationMinutes = WireFormats.TryMinutesOfDuration(workBreak.Duration);
        wire.CountedInReports = workBreak.CountedInReports;
        return wire;
    }

    /// <summary>Доменный текст отчёта в wire-форму.</summary>
    public static ReportWire TryFromReport(Report report)
    {
        var wire = new ReportWire();
        wire.Id = report.Id;
        wire.WorkDayId = report.WorkDayId;
        wire.Text = report.Text;
        wire.CreatedAt = WireFormats.TryTimestampText(report.CreatedAt);
        wire.UpdatedAt = WireFormats.TryTimestampText(report.UpdatedAt);
        return wire;
    }

    /// <summary>Доменный статус отсутствия в wire-форму.</summary>
    public static AbsenceWire TryFromAbsence(Absence absence)
    {
        var wire = new AbsenceWire();
        wire.Id = absence.Id;
        wire.UserId = absence.UserId;
        wire.Date = WireFormats.TryDateText(absence.Date);
        wire.Reason = absence.Reason.ToString();
        wire.CreatedByUserId = WireFormats.TryGuidText(absence.CreatedByUserId);
        return wire;
    }

    /// <summary>Доменная запись журнала событий в wire-форму.</summary>
    public static AuditLogWire TryFromAuditLog(AuditLog auditLog)
    {
        var wire = new AuditLogWire();
        wire.Id = auditLog.Id;
        wire.UserId = auditLog.UserId;
        wire.Action = auditLog.Action.ToString();
        wire.EntityType = auditLog.EntityType;
        wire.EntityId = auditLog.EntityId;
        wire.OldValue = auditLog.OldValue;
        wire.NewValue = auditLog.NewValue;
        wire.Timestamp = WireFormats.TryTimestampText(auditLog.Timestamp);
        return wire;
    }

    /// <summary>Записи журнала событий списком в wire-форму.</summary>
    public static AuditLogWire[] TryFromAuditLogs(IEnumerable<AuditLog> logs)
    {
        var result = new List<AuditLogWire>();
        foreach (var log in logs)
        {
            result.Add(TryFromAuditLog(log));
        }

        return result.ToArray();
    }

    /// <summary>DTO перерыва в wire-форму.</summary>
    public static BreakWire TryFromBreakDto(BreakDto dto)
    {
        var wire = new BreakWire();
        wire.Id = dto.Id;
        wire.WorkDayId = dto.WorkDayId;
        wire.StartTime = WireFormats.TryTimeOfDayText(dto.StartTime);
        wire.EndTime = WireFormats.TryTimeOfDayText(dto.EndTime);
        wire.DurationMinutes = WireFormats.TryMinutesOfDuration(dto.Duration);
        wire.CountedInReports = dto.CountedInReports;
        return wire;
    }

    /// <summary>DTO отчёта в wire-форму.</summary>
    public static ReportWire TryFromReportDto(ReportDto dto)
    {
        var wire = new ReportWire();
        wire.Id = dto.Id;
        wire.WorkDayId = dto.WorkDayId;
        wire.Text = dto.Text;
        wire.CreatedAt = WireFormats.TryTimestampText(dto.CreatedAt);
        wire.UpdatedAt = WireFormats.TryTimestampText(dto.UpdatedAt);
        return wire;
    }

    /// <summary>DTO отчётов списком в wire-форму.</summary>
    public static ReportWire[] TryFromReportDtos(IEnumerable<ReportDto> dtos)
    {
        var result = new List<ReportWire>();
        foreach (var dto in dtos)
        {
            result.Add(TryFromReportDto(dto));
        }

        return result.ToArray();
    }

    /// <summary>Дневный свод из Application-слоя в wire-форму.</summary>
    public static DailyReportWire TryFromDailyReport(DailyReport report)
    {
        var wire = new DailyReportWire();
        wire.StartTime = WireFormats.TryTimeOfDayText(report.StartTime);
        wire.EndTime = WireFormats.TryTimeOfDayText(report.EndTime);
        wire.TotalMinutes = WireFormats.TryMinutesOfSpan(report.TotalHours);

        foreach (var reportBreak in report.Breaks)
        {
            var breakWire = new ReportBreakWire();
            breakWire.StartTime = WireFormats.TryTimeOfDayText(reportBreak.StartTime);
            breakWire.EndTime = WireFormats.TryTimeOfDayText(reportBreak.EndTime);
            wire.Breaks.Add(breakWire);
        }

        return wire;
    }

    /// <summary>Месячные сводки списком в wire-форму; разреженные дни остаются разреженными (BR15, BR26, BR5).</summary>
    public static MonthlyReportWire[] TryFromMonthlyReports(IEnumerable<MonthlyReport> reports)
    {
        var result = new List<MonthlyReportWire>();

        foreach (var report in reports)
        {
            var wire = new MonthlyReportWire();
            wire.EmployeeName = report.EmployeeName;
            wire.TotalMinutes = WireFormats.TryMinutesOfSpan(report.TotalHours);

            foreach (var entry in report.HoursByDay)
            {
                wire.HoursByDay[WireFormats.TryDateText(entry.Key)] = WireFormats.TryMinutesOfSpan(entry.Value);
            }

            foreach (var day in report.AbsenceDays)
            {
                wire.AbsenceDays.Add(WireFormats.TryDateText(day));
            }

            result.Add(wire);
        }

        return result.ToArray();
    }

    /// <summary>DTO статуса отсутствия в wire-форму.</summary>
    public static AbsenceWire TryFromAbsenceDto(AbsenceDto dto)
    {
        var wire = new AbsenceWire();
        wire.Id = dto.Id;
        wire.UserId = dto.UserId;
        wire.Date = WireFormats.TryDateText(dto.Date);
        wire.Reason = dto.Reason.ToString();
        wire.CreatedByUserId = WireFormats.TryGuidText(dto.CreatedByUserId);
        return wire;
    }

    /// <summary>DTO статусов отсутствия списком в wire-форму.</summary>
    public static AbsenceWire[] TryFromAbsenceDtos(IEnumerable<AbsenceDto> dtos)
    {
        var result = new List<AbsenceWire>();
        foreach (var dto in dtos)
        {
            result.Add(TryFromAbsenceDto(dto));
        }

        return result.ToArray();
    }

    /// <summary>DTO записи журнала событий в wire-форму.</summary>
    public static AuditLogWire TryFromAuditLogDto(AuditLogDto dto)
    {
        var wire = new AuditLogWire();
        wire.Id = dto.Id;
        wire.UserId = dto.UserId;
        wire.Action = dto.Action.ToString();
        wire.EntityType = dto.EntityType;
        wire.EntityId = dto.EntityId;
        wire.OldValue = dto.OldValue;
        wire.NewValue = dto.NewValue;
        wire.Timestamp = WireFormats.TryTimestampText(dto.Timestamp);
        return wire;
    }

    /// <summary>DTO записей журнала событий списком в wire-форму.</summary>
    public static AuditLogWire[] TryFromAuditLogDtos(IEnumerable<AuditLogDto> dtos)
    {
        var result = new List<AuditLogWire>();
        foreach (var dto in dtos)
        {
            result.Add(TryFromAuditLogDto(dto));
        }

        return result.ToArray();
    }
}

// envelope error{code,message,httpStatus} строится в одном месте — ErrorEnvelope.TryEnvelope
// (Middleware.cs); текстовые builder'ы здесь были мёртвым кодом и удалены.




