// Расчётный период: закрытие и вскрытие. Бизнес-логика переехала сюда из AdminController (этап:
// контроллеры не зависят от репозиториев, только от сервисов). BR23.1 — закрытый период блокирует
// редактирование данных внутри него (BR12 проверяется в сервисе отчётов); BR23.2 — вскрытие
// закрытого периода требует причину / номер акта и логируется как «Экстренная корректировка».
// Личность актора проверяет контроллер через UserService, права по BR19 — политики Api-слоя;
// здесь — только данные периода и сами правила.
using WorkTime.Domain;

/// <summary>
/// Расчётный период: закрытие (BR23.1) и вскрытие администратором с обязательной причиной
/// (BR23.2). Права по BR19 проверяет Api-слой, здесь — только данные периода и сами правила.
/// </summary>
public class PeriodClosureService
{
    private readonly IPeriodClosureRepository _periodClosures;

    private readonly AuditLogService _auditLogs;

    /// <summary>
    /// Создаёт сервис поверх хранилища закрытий периода и журнала событий.
    /// </summary>
    /// <param name="periodClosures">Хранилище закрытий периода.</param>
    /// <param name="auditLogs">Журнал событий, куда пишется закрытие и вскрытие.</param>
    public PeriodClosureService(
        IPeriodClosureRepository periodClosures,
        AuditLogService auditLogs)
    {
        _periodClosures = periodClosures;
        _auditLogs = auditLogs;
    }

    // BR23.1: закрытие расчётного периода настраивается администратором; повторное закрытие
    // бессмысленно, поэтому сначала существование записи, затем её состояние.
    /// <summary>
    /// Закрывает расчётный период (BR23.1) — правка данных внутри него становится запрещена.
    /// </summary>
    /// <param name="periodClosureId">Период, который закрывают.</param>
    /// <param name="actorId">Администратор, который закрыл период.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Закрытая запись периода.</returns>
    /// <remarks>Отсутствующий id — EntityNotFoundException; повторное закрытие — BusinessRuleException BR23.1.</remarks>
    public async Task<PeriodClosure> ClosePeriodAsync(
        Guid periodClosureId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var closure = await TryRequireClosureAsync(periodClosureId, cancellationToken);

        if (closure.IsClosed)
        {
            throw new BusinessRuleException("BR23.1", "[RU] This billing period is already closed.");
        }

        closure.IsClosed = true;
        closure.ClosedAt = DateTime.UtcNow;
        closure.ClosedBy = actorId;

        await _periodClosures.SaveAsync(closure, cancellationToken);
        await _auditLogs.TryWriteAsync(
            actorId, AuditAction.Update, "PeriodClosure", periodClosureId, "isClosed=false",
            "isClosed=true (BR23.1)", cancellationToken);

        return closure;
    }

    // BR23.2: вскрытие закрытого периода требует причину / номер акта; разбор причины идёт первым —
    // тот же порядок, что был в контроллере (400 на пустой причины до 404 на неизвестном id).
    /// <summary>
    /// Вскрывает закрытый период (BR23.2) с обязательной причиной или номером акта; изменение
    /// логируется как «Экстренная корректировка».
    /// </summary>
    /// <param name="periodClosureId">Период, который вскрывают.</param>
    /// <param name="reason">Обязательная причина или номер акта.</param>
    /// <param name="actorId">Администратор, который вскрыл период.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Открытая запись периода с сохранённой причиной вскрытия.</returns>
    /// <remarks>Пустая причина разбирается раньше существования id — тот же порядок, что был в контроллере; открытый период — BusinessRuleException BR23.2.</remarks>
    public async Task<PeriodClosure> ReopenPeriodAsync(
        Guid periodClosureId,
        string? reason,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var reasonText = TryRequireText(reason, "reason");

        var closure = await TryRequireClosureAsync(periodClosureId, cancellationToken);

        if (!closure.IsClosed)
        {
            throw new BusinessRuleException("BR23.2", "[RU] Only a closed billing period can be reopened.");
        }

        closure.IsClosed = false;
        closure.ReopenReason = reasonText;
        closure.ReopenedByUserId = actorId;

        await _periodClosures.SaveAsync(closure, cancellationToken);
        await _auditLogs.TryWriteAsync(
            actorId, AuditAction.Update, "PeriodClosure", periodClosureId, "isClosed=true",
            "[RU] Emergency correction: " + reasonText, cancellationToken);

        return closure;
    }

    private async Task<PeriodClosure> TryRequireClosureAsync(
        Guid periodClosureId,
        CancellationToken cancellationToken)
    {
        var closure = await _periodClosures.GetByIdAsync(periodClosureId, cancellationToken);
        if (closure == null)
        {
            throw new EntityNotFoundException(periodClosureId, "PeriodClosure");
        }

        return closure;
    }

    // Синтаксический контроль поля, который раньше делал контроллер через RequestParsers:
    // то же сообщение и тот же код в paramName (Application-слой не ссылается на Api-слой).
    private static string TryRequireText(string? text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "[RU] Field '" + fieldName + "' is required.",
                paramName: "FIELD_MISSING");
        }

        return text!;
    }
}
