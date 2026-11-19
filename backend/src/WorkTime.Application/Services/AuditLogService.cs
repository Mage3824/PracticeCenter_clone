// Журнал событий (BR29/BR30): обёртка над IAuditLogRepository для Application-слоя. Чтение истории
// переехало сюда из AdminController (этап: контроллеры не зависят от репозиториев), запись — из
// Api-хелпера AuditTrail.TryWriteAsync. Формат записи не меняется: тот же Id, UserId, Action,
// EntityType, EntityId, OldValue/NewValue и Timestamp = DateTime.UtcNow. Существующие сервисы
// (UserService, DepartmentService) уже писали журнал своими приватными helpers и не переписаны —
// поведение подэтапов A/B осталось прежним, здесь только путь Admin-слоя.
using WorkTime.Domain;

/// <summary>
/// Журнал событий для Application-слоя: чтение истории сущности и запись административных
/// изменений поверх хранилища записей (BR29, BR30).
/// </summary>
public class AuditLogService
{
    private readonly IAuditLogRepository _auditLogs;

    /// <summary>
    /// Создаёт сервис поверх хранилища записей журнала.
    /// </summary>
    /// <param name="auditLogs">Хранилище записей журнала.</param>
    public AuditLogService(IAuditLogRepository auditLogs)
    {
        _auditLogs = auditLogs;
    }

    // BR30: история изменений одной сущности (кто, что, до/после, время). Вопрос репозитория
    // передаётся как критерий — список записей клиент не собирает.
    /// <summary>
    /// История изменений одной сущности: кто, что, до/после, время (BR30).
    /// </summary>
    /// <param name="entityId">Сущность, историю которой читают.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Записи истории этой сущности.</returns>
    public async Task<IEnumerable<AuditLog>> GetEntityHistoryAsync(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return await _auditLogs.GetHistoryForEntityAsync(entityId, cancellationToken);
    }

    // BR23/BR31: административные изменения пишутся в журнал событий (кто, что, до/после, время).
    /// <summary>
    /// Пишет административное изменение в журнал событий (BR23, BR31).
    /// </summary>
    /// <param name="actorId">Автор изменения.</param>
    /// <param name="action">Вид изменения.</param>
    /// <param name="entityType">Название типа изменённой сущности.</param>
    /// <param name="entityId">Сущность изменения.</param>
    /// <param name="oldValue">Состояние до изменения; null для создания.</param>
    /// <param name="newValue">Состояние после изменения; null для удаления.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    public async Task TryWriteAsync(
        Guid actorId,
        AuditAction action,
        string entityType,
        Guid entityId,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow,
        };

        await _auditLogs.SaveAsync(entry, cancellationToken);
    }
}
