// Формы запросов эндпоинтов управления (пользователи, отделы, комментарии, период).
// Валидация — DataAnnotations: невалидное тело отдаёт 400 как ValidationProblemDetails (RFC 7807).
using System.ComponentModel.DataAnnotations;

using System.Text.Json.Serialization;

namespace WorkTime.Api;

/// <summary>Тело создания учётной записи.</summary>
public class CreateUserRequest
{
    /// <summary>Логин новой учётной записи.</summary>
    [Required]
    [JsonPropertyName("login")] public string? Login { get; set; }

    /// <summary>Отображаемое имя.</summary>
    [Required]
    [JsonPropertyName("fullName")] public string? FullName { get; set; }

    // BR34: пароль приходит только в запросе и никогда не возвращается в ответах.
    /// <summary>Пароль: приходит только в запросе и никогда не возвращается (BR34).</summary>
    [Required]
    [JsonPropertyName("password")] public string? Password { get; set; }

    // Токен роли как в Enums.cs (Admin / Manager / Employee).
    /// <summary>Токен роли ровно как в перечислении ролей.</summary>
    [Required]
    [JsonPropertyName("role")] public string? Role { get; set; }

    /// <summary>Подразделение, которому принадлежит учётная запись.</summary>
    [Required]
    [JsonPropertyName("departmentId")] public Guid? DepartmentId { get; set; }
}

/// <summary>Тело правки учётной записи; поля меняются только когда присланы.</summary>
public class UpdateUserRequest
{
    /// <summary>Новое отображаемое имя.</summary>
    [JsonPropertyName("fullName")] public string? FullName { get; set; }

    /// <summary>Новый токен роли.</summary>
    [JsonPropertyName("role")] public string? Role { get; set; }

    /// <summary>Новое подразделение.</summary>
    [JsonPropertyName("departmentId")] public Guid? DepartmentId { get; set; }

    // BR24: снятие доступа без удаления учётной записи; true возвращает доступ.
    /// <summary>Снятие или возврат доступа без удаления учётной записи (BR24).</summary>
    [JsonPropertyName("isActive")] public bool? IsActive { get; set; }
}

/// <summary>Тело создания подразделения.</summary>
public class CreateDepartmentRequest
{
    /// <summary>Имя подразделения.</summary>
    [Required]
    [JsonPropertyName("name")] public string? Name { get; set; }

    /// <summary>Родитель дерева; null для узла верхнего уровня.</summary>
    [JsonPropertyName("parentId")] public Guid? ParentId { get; set; }
}

/// <summary>Тело правки подразделения; поля меняются только когда присланы.</summary>
public class UpdateDepartmentRequest
{
    /// <summary>Новое имя подразделения.</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }

    /// <summary>Новый родитель дерева; null делает узлом верхнего уровня.</summary>
    [JsonPropertyName("parentId")] public Guid? ParentId { get; set; }
}

/// <summary>Тело комментария руководителя к отчёту (BR13).</summary>
public class CreateCommentRequest
{
    /// <summary>Отчёт, к которому относится комментарий.</summary>
    [Required]
    [JsonPropertyName("reportId")] public Guid? ReportId { get; set; }

    /// <summary>Текст комментария.</summary>
    [Required]
    [JsonPropertyName("text")] public string? Text { get; set; }
}

// BR23.2: вскрытия закрытого периода требует причину / номер акта.
/// <summary>Тело вскрытия закрытого расчётного периода (BR23.2).</summary>
public class ReopenPeriodRequest
{
    /// <summary>Причина или номер акта экстренной корректировки.</summary>
    [Required]
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}



