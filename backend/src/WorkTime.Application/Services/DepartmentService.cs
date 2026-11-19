// Отделы: CRUD. Бизнес-логика переехала сюда из DepartmentsController (этап: контроллеры не
// зависят от репозиториев, только от сервисов). Правила: дерево отделов BR16/BR19 — родитель
// должен существовать, отдел не может быть собственным родителем (SELF_PARENT); удаление отдела
// с подчинёнными оставило бы их ParentId висящим, а отдел с активными сотрудниками — ссылки на
// несуществующий отдел, поэтому сначала дерево и люди. BR29/BR30 (каждая административная правка
// пишется в журнал событий). Права по BR19 проверяются политиками авторизации Api-слоя,
// здесь — только данные и правила.
using WorkTime.Domain;

/// <summary>
/// Отделы: CRUD поверх хранилища отделов, людей и журнала событий. Дерево BR16/BR19 — родитель
/// должен существовать и не быть самим отделом; удаление сначала проверяет детей и сотрудников.
/// Права по BR19 проверяет Api-слой, здесь — только данные и правила.
/// </summary>
public class DepartmentService
{
    private readonly IDepartmentRepository _departments;

    private readonly IUserRepository _users;

    private readonly IAuditLogRepository _auditLogs;

    /// <summary>
    /// Создаёт сервис поверх хранилища отделов, хранилища людей (гейт удаления) и журнала событий.
    /// </summary>
    /// <param name="departments">Хранилище отделов.</param>
    /// <param name="users">Хранилище людей: отдел с активными сотрудниками не удаляется.</param>
    /// <param name="auditLogs">Журнал событий BR29/BR30.</param>
    public DepartmentService(
        IDepartmentRepository departments,
        IUserRepository users,
        IAuditLogRepository auditLogs)
    {
        _departments = departments;
        _users = users;
        _auditLogs = auditLogs;
    }

    // Весь список отделов: без этого верхние отделы (ParentId = null) недостижимы через детей.
    /// <summary>
    /// Весь список отделов: верхние узлы (ParentId = null) иначе недостижимы через детей.
    /// </summary>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Все отделы.</returns>
    public async Task<IEnumerable<Department>> ListAsync(CancellationToken cancellationToken)
    {
        return await _departments.GetAllAsync(cancellationToken);
    }

    // Существование отдела: неизвестный id — 404 (тот же путь используется для GET, для детей
    // и для проверки родителя при создании и правке).
    /// <summary>
    /// Отдел по id; тот же путь используется для детей и для проверки родителя при создании и правке.
    /// </summary>
    /// <param name="departmentId">Искомый отдел.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Найденный отдел.</returns>
    /// <remarks>Отсутствующий id — EntityNotFoundException (404).</remarks>
    public async Task<Department> GetAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        return await TryRequireDepartmentAsync(departmentId, cancellationToken);
    }

    // Дети отдела (дерево BR16/BR19): родитель читается перед списком — тот же порядок, что был
    // в контроллере (несуществующий родитель даёт 404, а не пустой список).
    /// <summary>
    /// Непосредственные дети отдела — дерево BR16/BR19.
    /// </summary>
    /// <param name="departmentId">Родитель, детей которого читают.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Дети отдела.</returns>
    /// <remarks>Родитель читается перед списком: несуществующий родитель даёт 404, а не пустой список.</remarks>
    public async Task<IEnumerable<Department>> ListChildrenAsync(
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        await TryRequireDepartmentAsync(departmentId, cancellationToken);
        return await _departments.GetChildrenOfAsync(departmentId, cancellationToken);
    }

    /// <summary>
    /// Создаёт отдел из черновика (BR16/BR19) и пишет изменение в журнал (BR29/BR30).
    /// </summary>
    /// <param name="draft">Имя и родитель нового отдела.</param>
    /// <param name="actorId">Администратор, создающий отдел.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Созданный отдел.</returns>
    /// <remarks>Пустое имя — ArgumentException FIELD_MISSING; отсутствующий родитель — EntityNotFoundException; отдел, указанный как собственный родитель — BusinessRuleException SELF_PARENT.</remarks>
    public async Task<Department> CreateAsync(
        CreateDepartmentDraft draft,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var name = TryRequireText(draft.Name, "name");
        var parentId = draft.ParentId;

        if (parentId != null)
        {
            await TryRequireDepartmentAsync(parentId.Value, cancellationToken);
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentId = parentId,
        };

        await _departments.SaveAsync(department, cancellationToken);
        await TryWriteAuditAsync(
            actorId, AuditAction.Create, department.Id, null, "name=" + department.Name, cancellationToken);

        return department;
    }

    /// <summary>
    /// Правка отдела из черновика: поля, которые не пришли, остаются прежними.
    /// </summary>
    /// <param name="departmentId">Правимый отдел.</param>
    /// <param name="draft">Новые имя и родитель; null означает «оставить как было».</param>
    /// <param name="actorId">Администратор, который правит.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Правленный отдел.</returns>
    /// <remarks>Синтаксические ошибки проверяются до мутации; новый родитель должен существовать и не быть самим отделом.</remarks>
    public async Task<Department> UpdateAsync(
        Guid departmentId,
        UpdateDepartmentDraft draft,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var department = await TryRequireDepartmentAsync(departmentId, cancellationToken);
        var before = "name=" + department.Name + ", parentId=" + department.ParentId;

        if (draft.Name != null)
        {
            department.Name = TryRequireText(draft.Name, "name");
        }

        if (draft.ParentId != null)
        {
            if (draft.ParentId.Value == departmentId)
            {
                throw new ArgumentException(
                    "[RU] A department cannot be its own parent.",
                    paramName: "SELF_PARENT");
            }

            await TryRequireDepartmentAsync(draft.ParentId.Value, cancellationToken);
            department.ParentId = draft.ParentId.Value;
        }

        await _departments.SaveAsync(department, cancellationToken);
        await TryWriteAuditAsync(
            actorId, AuditAction.Update, department.Id, before,
            "name=" + department.Name + ", parentId=" + department.ParentId, cancellationToken);

        return department;
    }

    /// <summary>
    /// Удаляет отдел, сначала проверив дерево и людей (BR16/BR19, BR29/BR30).
    /// </summary>
    /// <param name="departmentId">Удаляемый отдел.</param>
    /// <param name="actorId">Администратор, который удаляет.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <remarks>Отдел с подчинёнными или с активными сотрудниками не удаляется: иначе повисают ParentId детей и ссылки на несуществующий отдел.</remarks>
    public async Task DeleteAsync(Guid departmentId, Guid actorId, CancellationToken cancellationToken)
    {
        var department = await TryRequireDepartmentAsync(departmentId, cancellationToken);

        // Удаление отдела с подчинёнными оставило бы их ParentId висящим — сначала дерево.
        var children = await _departments.GetChildrenOfAsync(departmentId, cancellationToken);
        foreach (var unused in children)
        {
            throw new BusinessRuleException(
                "DEPARTMENT_HAS_CHILDREN",
                "[RU] Move or delete the subordinate departments before deleting this one.");
        }

        var employees = await _users.GetActiveEmployeesInDepartmentAsync(departmentId, cancellationToken);
        foreach (var unused in employees)
        {
            throw new BusinessRuleException(
                "DEPARTMENT_HAS_EMPLOYEES",
                "[RU] Move the employees of this department to another department first.");
        }

        await _departments.DeleteAsync(departmentId, cancellationToken);
        await TryWriteAuditAsync(
            actorId, AuditAction.Delete, departmentId, "name=" + department.Name, null, cancellationToken);
    }

    private async Task<Department> TryRequireDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _departments.GetByIdAsync(departmentId, cancellationToken);
        if (department == null)
        {
            throw new EntityNotFoundException(departmentId, "Department");
        }

        return department;
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

    // BR23/BR31: административные изменения пишутся в журнал событий (кто, что, до/после, время).
    private async Task TryWriteAuditAsync(
        Guid actorId,
        AuditAction action,
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
            EntityType = "Department",
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow,
        };

        await _auditLogs.SaveAsync(entry, cancellationToken);
    }
}
