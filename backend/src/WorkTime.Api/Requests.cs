// Тела запросов WorkTime.Api (этап 2). Это «проводные» модели стороны запроса: ключи JSON задаются
// атрибутом [JsonPropertyName] и ровно повторяют ключи api-contracts.md §5/§7 (имена свойств в C# —
// PascalCase), и они сознательно не содержат поля employeeId / currentUserId / editorId / actorId —
// личность всегда берётся из субъекта токена (см. api-contracts.md §6: клиент присылает критерии,
// а не список id).
//
// Значения идут как скаляры контракта ("YYYY-MM-DD", "HH:mm", имена перечислений), и RequestParsers
// ниже превращает их в скаляры домена, отбрасывая некорректное через ArgumentException -> HTTP 400
// + тот же error envelope, который используют бизнес-правила.
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WorkTime.Domain;

using System.Text.Json.Serialization;

namespace WorkTime.Api;

// Syntax-only patterns from api-contracts.md §3. They check the shape a client sends, never a rule.
/// <summary>Синтаксические регулярки: проверяют форму, которую присылает клиент, и никогда — бизнес-правило.</summary>
public static class RequestPatterns
{
    /// <summary>Форма календарной даты «YYYY-MM-DD».</summary>
    public const string DatePattern = @"^\d{4}-\d{2}-\d{2}$";

    /// <summary>Форма времени суток «HH:mm» в 24-часовом диапазоне.</summary>
    public const string TimeOfDayPattern = @"^([01]\d|2[0-3]):[0-5]\d$";

    /// <summary>Текст подсказки для неверной даты.</summary>
    public const string DateErrorMessage = "[RU] Field must be a date in YYYY-MM-DD form.";

    /// <summary>Текст подсказки для неверного времени суток.</summary>
    public const string TimeOfDayErrorMessage = "[RU] Field must be a time of day in HH:mm form.";
}

/// <summary>Тело запроса входа.</summary>
public class LoginRequest
{
    /// <summary>Логин учётной записи.</summary>
    [Required]
    [JsonPropertyName("login")] public string? Login { get; set; }

    /// <summary>Пароль: приходит только в запросе и никогда не возвращается в ответах (BR34).</summary>
    [Required]
    [JsonPropertyName("password")] public string? Password { get; set; }
}

/// <summary>Тело открытия рабочего дня.</summary>
public class StartWorkDayRequest
{
    /// <summary>Календарная дата дня.</summary>
    [Required]
    [RegularExpression(RequestPatterns.DatePattern, ErrorMessage = RequestPatterns.DateErrorMessage)]
    [JsonPropertyName("date")] public string? Date { get; set; }

    /// <summary>Время открытия смены.</summary>
    [Required]
    [RegularExpression(RequestPatterns.TimeOfDayPattern, ErrorMessage = RequestPatterns.TimeOfDayErrorMessage)]
    [JsonPropertyName("startTime")] public string? StartTime { get; set; }
}

/// <summary>Тело закрытия рабочего дня.</summary>
public class FinishWorkDayRequest
{
    /// <summary>Время закрытия смены.</summary>
    [Required]
    [RegularExpression(RequestPatterns.TimeOfDayPattern, ErrorMessage = RequestPatterns.TimeOfDayErrorMessage)]
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }
}

/// <summary>Тело начала перерыва.</summary>
public class StartBreakRequest
{
    /// <summary>Время начала перерыва.</summary>
    [Required]
    [RegularExpression(RequestPatterns.TimeOfDayPattern, ErrorMessage = RequestPatterns.TimeOfDayErrorMessage)]
    [JsonPropertyName("startTime")] public string? StartTime { get; set; }
}

/// <summary>Тело закрытия перерыва.</summary>
public class FinishBreakRequest
{
    /// <summary>Время закрытия перерыва.</summary>
    [Required]
    [RegularExpression(RequestPatterns.TimeOfDayPattern, ErrorMessage = RequestPatterns.TimeOfDayErrorMessage)]
    [JsonPropertyName("endTime")] public string? EndTime { get; set; }
}

/// <summary>Тело текста отчёта.</summary>
public class CreateReportRequest
{
    // BR12 makes the text mandatory, but no BR fixes a length, so no StringLength is invented here.
    /// <summary>Текст отчёта: BR12 требует наличия поля, длину ни одно BR не задаёт.</summary>
    [Required]
    [JsonPropertyName("text")] public string? Text { get; set; }
}

/// <summary>Тело установки статуса отсутствия.</summary>
public class SetAbsenceRequest
{
    /// <summary>Календарная дата, которую покрывает статус.</summary>
    [Required]
    [RegularExpression(RequestPatterns.DatePattern, ErrorMessage = RequestPatterns.DateErrorMessage)]
    [JsonPropertyName("date")] public string? Date { get; set; }

    // The allowed tokens come from AbsenceReason itself (see TryParseAbsenceReason), so a new
    // enumerator cannot be rejected by a hard-coded pattern here.
    /// <summary>Токен причины из перечислителя AbsenceReason — список допустимых значений берётся из самого перечислителя.</summary>
    [Required]
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}

// Wire scalar -> domain scalar, with the 400 envelope for anything unusable. Nothing here consults a
// repository: this is request syntax, not a business rule.
/// <summary>Разбор скаляров запроса в доменные скаляры с конвертом 400 для непригодного значения; ничего не читает из хранилища — это синтаксис запроса, не бизнес-правило.</summary>
public class RequestParsers
{
    private const string DateFormat = "yyyy-MM-dd";

    // "YYYY-MM-DD" -> a date whose time part is zero (the domain stores dates that way).
    /// <summary>Текст «YYYY-MM-DD» в дату с нулевой временной частью — домен хранит даты именно так.</summary>
    public static DateTime TryParseDateText(string? text, string fieldName)
    {
        TryRequireText(text, fieldName);

        if (!DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ArgumentException(
                "[RU] Field '" + fieldName + "' must be a date in YYYY-MM-DD form.",
                paramName: "DATE_INVALID");
        }

        return date.Date;
    }

    // "HH:mm" -> a time of day. Seconds are not on the wire, so they are never produced.
    /// <summary>Текст «HH:mm» в время суток; секунд на проводе нет, поэтому они никогда не появляются.</summary>
    public static TimeSpan TryParseTimeOfDayText(string? text, string fieldName)
    {
        TryRequireText(text, fieldName);

        var parts = text!.Split(':');
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                "[RU] Field '" + fieldName + "' must be a time of day in HH:mm form.",
                paramName: "TIME_INVALID");
        }

        var hours = TryParseSmallInteger(parts[0]);
        var minutes = TryParseSmallInteger(parts[1]);

        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
        {
            throw new ArgumentException(
                "[RU] Field '" + fieldName + "' must be a time of day in HH:mm form.",
                paramName: "TIME_INVALID");
        }

        return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
    }

    // Enum name exactly as in Enums.cs (BR: never send the ordinal). The token list comes from the
    // enum itself, so adding an enumerator cannot silently invalidate a controller.
    /// <summary>Имя члена перечислителя ровно как в перечислении — порядковый номер никогда не отправляется; список токенов берётся из самого перечислителя.</summary>
    public static TEnum TryParseEnum<TEnum>(string? text, string fieldName, string code)
        where TEnum : struct, Enum
    {
        TryRequireText(text, fieldName);

        var names = Enum.GetNames<TEnum>();
        foreach (var name in names)
        {
            if (string.Equals(name, text, StringComparison.Ordinal))
            {
                return Enum.Parse<TEnum>(name);
            }
        }

        throw new ArgumentException(
            "[RU] Field '" + fieldName + "' must be one of: " + string.Join(", ", names) + ".",
            paramName: code);
    }

    /// <summary>Токен причины отсутствия из перечислителя AbsenceReason.</summary>
    public static AbsenceReason TryParseAbsenceReason(string? text)
    {
        return TryParseEnum<AbsenceReason>(text, "reason", "REASON_INVALID");
    }

    // A text field the endpoint needs (login, report text is allowed to be empty only where the
    // contract says so).
    /// <summary>Текстовое поле, которое эндпоинт обязан получить: пустой текст разрешён только там, где это говорит контракт.</summary>
    public static string TryRequireText(string? text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "[RU] Field '" + fieldName + "' is required.",
                paramName: "FIELD_MISSING");
        }

        return text!;
    }

    private static int TryParseSmallInteger(string text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
    }
}



