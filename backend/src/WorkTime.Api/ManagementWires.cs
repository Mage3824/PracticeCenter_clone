// Проводные формы сущностей управления (пользователи, отделы, комментарии, расчётный период),
// конвертеры сущностей домена и вспомогательные средства для контроллеров этапа 2.
// Формы повторяют таблицы полей api-contracts.md (§5); PasswordHash наружу не отдаётся (BR34).
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WorkTime.Domain;

using System.Text.Json.Serialization;

namespace WorkTime.Api;

/// <summary>Учётная запись на проводе — административный вид, без пароля (BR34).</summary>
public class UserWire
{
    /// <summary>Идентификатор учётной записи.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }
    /// <summary>Логин учётной записи.</summary>
    [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
    /// <summary>Отображаемое имя.</summary>
    [JsonPropertyName("fullName")] public string FullName { get; set; } = string.Empty;
    /// <summary>Токен роли из перечисления ролей.</summary>
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    /// <summary>Подразделение учётной записи.</summary>
    [JsonPropertyName("departmentId")] public Guid DepartmentId { get; set; }
    /// <summary>Доступ разрешён; false — доступ снят при сохранении записи и всей истории (BR24).</summary>
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    /// <summary>Неудачные входы за текущий день (BR35).</summary>
    [JsonPropertyName("failedLoginAttempts")] public int FailedLoginAttempts { get; set; }
    /// <summary>Момент, до которого вход закрыт; null когда блокировки нет.</summary>
    [JsonPropertyName("lockedUntil")] public string? LockedUntil { get; set; }
    /// <summary>Блокировка до конца дня — снимает только администратор (BR35).</summary>
    [JsonPropertyName("locked")] public bool Locked { get; set; }
}

/// <summary>Узел дерева подразделений на проводе.</summary>
public class DepartmentWire
{
    /// <summary>Идентификатор подразделения.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }
    /// <summary>Имя подразделения.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Текст Guid родителя; null для узла верхнего уровня.</summary>
    [JsonPropertyName("parentId")] public string? ParentId { get; set; }
}

/// <summary>Комментарий руководителя к отчёту на проводе (BR13).</summary>
public class CommentWire
{
    /// <summary>Идентификатор комментария.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }
    /// <summary>Отчёт, к которому относится комментарий.</summary>
    [JsonPropertyName("reportId")] public Guid ReportId { get; set; }
    /// <summary>Автор комментария — руководитель или администратор.</summary>
    [JsonPropertyName("managerId")] public Guid ManagerId { get; set; }
    /// <summary>Текст комментария.</summary>
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    /// <summary>Момент создания — серверное время без суффикса часовой зоны.</summary>
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>Расчётный период на проводе (BR23.1, BR23.2).</summary>
public class PeriodClosureWire
{
    /// <summary>Идентификатор периода.</summary>
    [JsonPropertyName("id")] public Guid Id { get; set; }
    /// <summary>Подразделение, для которого настроен период.</summary>
    [JsonPropertyName("departmentId")] public Guid DepartmentId { get; set; }
    /// <summary>Год расчётного периода.</summary>
    [JsonPropertyName("year")] public int Year { get; set; }
    /// <summary>Месяц расчётного периода.</summary>
    [JsonPropertyName("month")] public int Month { get; set; }
    /// <summary>Период закрыт: правки данных внутри него блокируются (BR12).</summary>
    [JsonPropertyName("isClosed")] public bool IsClosed { get; set; }
    /// <summary>Момент закрытия; null пока период открыт.</summary>
    [JsonPropertyName("closedAt")] public string? ClosedAt { get; set; }
    /// <summary>Текст Guid администратора, закрывшего период; null пока период открыт.</summary>
    [JsonPropertyName("closedBy")] public string? ClosedBy { get; set; }
    /// <summary>Причина вскрытия, сохранённая после экстренной корректировки (BR23.2).</summary>
    [JsonPropertyName("reopenReason")] public string? ReopenReason { get; set; }
    /// <summary>Текст Guid администратора, вскрышего период; null когда вскрытия не было.</summary>
    [JsonPropertyName("reopenedByUserId")] public string? ReopenedByUserId { get; set; }
}

// Ответ эндпоинта GET /api/work-days/{employeeId}/can-start (BR27 как состояние кнопки).
/// <summary>Ответ состояния кнопки «Начать работу»: BR27 как флаг, а не как ошибка.</summary>
public class CanStartWorkDayWire
{
    /// <summary>Смену открыть можно; false — день помечен отсутствием (BR27).</summary>
    [JsonPropertyName("canStart")] public bool CanStart { get; set; }
}

/// <summary>Перевод административных сущностей в wire-формы; скаляры форматуруются тем же WireFormats.</summary>
public static class ManagementWireConversion
{
    /// <summary>Учётная запись в wire-форму: хеш пароля на провод никогда не выходит.</summary>
    public static UserWire TryFromUser(User user)
    {
        var wire = new UserWire();
        wire.Id = user.Id;
        wire.Login = user.Login;
        wire.FullName = user.FullName;
        wire.Role = user.Role.ToString();
        wire.DepartmentId = user.DepartmentId;
        wire.IsActive = user.IsActive;
        wire.FailedLoginAttempts = user.FailedLoginAttempts;
        wire.LockedUntil = WireFormats.TryTimestampText(user.LockedUntil);
        wire.Locked = user.Locked;
        return wire;
    }

    /// <summary>Учётные записи списком в wire-форму.</summary>
    public static UserWire[] TryFromUsers(IEnumerable<User> users)
    {
        var result = new List<UserWire>();
        foreach (var user in users)
        {
            result.Add(TryFromUser(user));
        }

        return result.ToArray();
    }

    /// <summary>Подразделение в wire-форму.</summary>
    public static DepartmentWire TryFromDepartment(Department department)
    {
        var wire = new DepartmentWire();
        wire.Id = department.Id;
        wire.Name = department.Name;
        wire.ParentId = WireFormats.TryGuidText(department.ParentId);
        return wire;
    }

    /// <summary>Подразделения списком в wire-форму.</summary>
    public static DepartmentWire[] TryFromDepartments(IEnumerable<Department> departments)
    {
        var result = new List<DepartmentWire>();
        foreach (var department in departments)
        {
            result.Add(TryFromDepartment(department));
        }

        return result.ToArray();
    }

    /// <summary>Комментарий в wire-форму.</summary>
    public static CommentWire TryFromComment(Comment comment)
    {
        var wire = new CommentWire();
        wire.Id = comment.Id;
        wire.ReportId = comment.ReportId;
        wire.ManagerId = comment.ManagerId;
        wire.Text = comment.Text;
        wire.CreatedAt = WireFormats.TryTimestampText(comment.CreatedAt);
        return wire;
    }

    /// <summary>Комментарии списком в wire-форму.</summary>
    public static CommentWire[] TryFromComments(IEnumerable<Comment> comments)
    {
        var result = new List<CommentWire>();
        foreach (var comment in comments)
        {
            result.Add(TryFromComment(comment));
        }

        return result.ToArray();
    }

    /// <summary>Расчётный период в wire-форму.</summary>
    public static PeriodClosureWire TryFromPeriodClosure(PeriodClosure closure)
    {
        var wire = new PeriodClosureWire();
        wire.Id = closure.Id;
        wire.DepartmentId = closure.DepartmentId;
        wire.Year = closure.Year;
        wire.Month = closure.Month;
        wire.IsClosed = closure.IsClosed;
        wire.ClosedAt = WireFormats.TryTimestampText(closure.ClosedAt);
        wire.ClosedBy = WireFormats.TryGuidText(closure.ClosedBy);
        wire.ReopenReason = closure.ReopenReason;
        wire.ReopenedByUserId = WireFormats.TryGuidText(closure.ReopenedByUserId);
        return wire;
    }
}

// BR17: Excel из этапа 4 ещё не построен, поэтому экспорт отдаётся как CSV (UTF-8 с BOM,
// разделитель ";" — так файл открывается в Excel на русской локали).
/// <summary>Месячная сводка как CSV: UTF-8 с BOM и разделитель «;», чтобы файл открывался в Excel на русской локали.</summary>
public static class MonthlyReportCsv
{
    /// <summary>Строит байты CSV из месячных сводок; сводок нет — файл остаётся пустым.</summary>
    public static byte[] TryBuild(MonthlyReportWire[] reports)
    {
        var dayKeys = new List<string>();
        foreach (var report in reports)
        {
            foreach (var key in report.HoursByDay.Keys)
            {
                if (!dayKeys.Contains(key))
                {
                    dayKeys.Add(key);
                }
            }
        }

        dayKeys.Sort(StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.Append("employee;").Append(string.Join(";", dayKeys)).Append(";total_minutes;absences");
        builder.Append("\r\n");

        foreach (var report in reports)
        {
            builder.Append(Escape(report.EmployeeName));

            foreach (var key in dayKeys)
            {
                var minutes = report.HoursByDay.TryGetValue(key, out var value) ? value : 0L;
                builder.Append(';').Append(minutes.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(';').Append(report.TotalMinutes.ToString(CultureInfo.InvariantCulture));
            builder.Append(';').Append(Escape(string.Join("|", report.AbsenceDays)));
            builder.Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string text)
    {
        if (!text.Contains(';') && !text.Contains('"') && !text.Contains('\r') && !text.Contains('\n'))
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}




