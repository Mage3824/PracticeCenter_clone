// Пользователи: CRUD, деактивация (BR24), разблокировка (BR35). Бизнес-логика переехала сюда
// из UsersController (этап: контроллеры не зависят от репозиториев, только от сервисов).
// Правила: BR20 (в системе должен остаться хотя бы один активный администратор), BR24
// (учётная запись сохраняется, доступ блокируется), BR34 (пароль пишется хешем, открытый
// пароль не храним и не отдаётся), BR35 (досрочная разблокировка — право администратора),
// BR29/BR30 (каждая административная правка пишется в журнал событий).
// Права по BR19 проверяются политиками авторизации Api-слоя, здесь — только данные и правила.
using WorkTime.Domain;

/// <summary>
/// Учётные записи: CRUD, деактивация (BR24), досрочная разблокировка (BR35) и гейт BR20 —
/// в системе должен остаться хотя бы один активный администратор. Пароль пишется хешем (BR34),
/// каждая административная правка идёт в журнал (BR29/BR30). Права по BR19 проверяет Api-слой.
/// </summary>
public class UserService
{
    private readonly IUserRepository _users;

    private readonly IDepartmentRepository _departments;

    private readonly IAuditLogRepository _auditLogs;

    /// <summary>
    /// Создаёт сервис поверх хранилища учётных записей, хранилища отделов и журнала событий.
    /// </summary>
    /// <param name="users">Хранилище учётных записей.</param>
    /// <param name="departments">Хранилище отделов: отдел новой записи должен существовать.</param>
    /// <param name="auditLogs">Журнал событий BR29/BR30.</param>
    public UserService(IUserRepository users, IDepartmentRepository departments, IAuditLogRepository auditLogs)
    {
        _users = users;
        _departments = departments;
        _auditLogs = auditLogs;
    }

    // BR24: деактивированные учётные записи остаются, поэтому список по умолчанию активный,
    // а полный список (includeInactive) нужен администратору для обслуживания.
    /// <summary>
    /// Список учётных записей по имени; по умолчанию только активные (BR24).
    /// </summary>
    /// <param name="name">Фильтр по отображаемому имени; null или пусто — без фильтра.</param>
    /// <param name="includeInactive">true добавляет деактивированные — нужно администратору для обслуживания.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Записи, подходящие под критерии.</returns>
    public async Task<IEnumerable<User>> ListUsersAsync(
        string? name,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        if (includeInactive)
        {
            return await _users.GetAllAsync(cancellationToken);
        }

        var found = await _users.GetActiveByNameAsync(name ?? string.Empty, cancellationToken);
        return found.Values;
    }

    // Существование учётной записи: неизвестный id — 404, никогда не созданная запись.
    // Тот же путь используется для проверки личности субъекта токена (RequestIdentity).
    /// <summary>
    /// Учётная запись по id; тот же путь проверяет личность субъекта токена.
    /// </summary>
    /// <param name="userId">Искомая учётная запись.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Найденная учётная запись.</returns>
    /// <remarks>Неизвестный id — EntityNotFoundException (404).</remarks>
    public async Task<User> GetKnownUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException(userId, "User");
        }

        return user;
    }

    // Активные сотрудники одного отдела (BR16 rows are chosen by the repository, never assembled
    // by a caller). Used by the department-delete gate: a department with employees is not
    // removable, so the controller asks UserService instead of the user repository.
    /// <summary>
    /// Активные сотрудники одного отдела — строки отчёта BR16, которые выбирает репозиторий.
    /// </summary>
    /// <param name="departmentId">Отдел; Guid.Empty означает всех активных сотрудников системы.</param>
    /// <param name="cancellationToken">Отменяет обращение к хранилищу.</param>
    /// <returns>Активные сотрудники отдела.</returns>
    /// <remarks>Используется гейтом удаления отдела: отдел с людьми не удаляется.</remarks>
    public async Task<IEnumerable<User>> GetActiveEmployeesInDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        return await _users.GetActiveEmployeesInDepartmentAsync(departmentId, cancellationToken);
    }

    /// <summary>
    /// Создаёт учётную запись из черновика; пароль пишется хешем (BR34), правка идёт в журнал (BR29/BR30).
    /// </summary>
    /// <param name="draft">Логин, имя, пароль, роль и отдел новой записи.</param>
    /// <param name="actorId">Администратор, создающий запись.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Созданная запись — с хешем вместо открытого пароля.</returns>
    /// <remarks>Пустые поля — ArgumentException с кодом в paramName; неизвестный отдел — EntityNotFoundException; роль вне списка — свой код.</remarks>
    public async Task<User> CreateUserAsync(
        CreateUserDraft draft,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var login = TryRequireText(draft.Login, "login");
        var fullName = TryRequireText(draft.FullName, "fullName");
        var password = TryRequireText(draft.Password, "password");
        var role = TryParseUserRole(draft.Role);
        var departmentId = draft.DepartmentId ?? throw new ArgumentException(
            "[RU] Field 'departmentId' is required.",
            paramName: "DEPARTMENT_ID_REQUIRED");

        var department = await _departments.GetByIdAsync(departmentId, cancellationToken);
        if (department == null)
        {
            throw new EntityNotFoundException(departmentId, "Department");
        }

        var taken = await _users.GetByLoginAsync(login, cancellationToken);
        if (taken != null)
        {
            throw new BusinessRuleException("USER_LOGIN_TAKEN", "[RU] This login is already used by another account.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            // BR34: пароль пишется хешем PasswordHasher — открытый пароль не храним.
            PasswordHash = PasswordHasher.TryHash(password),
            FullName = fullName,
            Role = role,
            DepartmentId = departmentId,
            IsActive = true,
            FailedLoginAttempts = 0,
            Locked = false,
            LockedUntil = null,
        };

        await _users.SaveAsync(user, cancellationToken);

        await TryWriteAuditAsync(actorId, AuditAction.Create, user.Id, null, TryUserSummary(user), cancellationToken);

        return user;
    }

    /// <summary>
    /// Правка учётной записи из черновика: поля, которые не пришли, остаются прежними.
    /// </summary>
    /// <param name="userId">Правимая учётная запись.</param>
    /// <param name="draft">Новые роль, активность, отдел и имя; null означает «оставить как было».</param>
    /// <param name="actorId">Администратор, который правит.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Правленная запись.</returns>
    /// <remarks>Значения проверяются до мутации; правка, снимающая роль администратора или деактивирующая его, проходит гейт BR20 по состоянию ДО правки.</remarks>
    public async Task<User> UpdateUserAsync(
        Guid userId,
        UpdateUserDraft draft,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var user = await GetKnownUserAsync(userId, cancellationToken);
        var before = TryUserSummary(user);

        // Значения готовятся до правки: сначала синтаксические ошибки (400), потом мутация.
        var nextRole = draft.Role == null
            ? user.Role
            : TryParseUserRole(draft.Role);
        var nextIsActive = draft.IsActive ?? user.IsActive;

        Guid? nextDepartmentId = null;
        if (draft.DepartmentId != null)
        {
            var department = await _departments.GetByIdAsync(draft.DepartmentId.Value, cancellationToken);
            if (department == null)
            {
                throw new EntityNotFoundException(draft.DepartmentId.Value, "Department");
            }

            nextDepartmentId = draft.DepartmentId.Value;
        }

        // BR20: правка снимает роль администратора или деактивирует его (состояние ДО правки).
        var removesActiveAdmin = user.Role == UserRole.Admin && user.IsActive
            && (nextRole != UserRole.Admin || !nextIsActive);

        // Правка аудита 3.2/3.3: мьютекс учётных записей убран, гейт BR20 и запись идут через await.
        if (removesActiveAdmin)
        {
            await TryGuardLastActiveAdminAsync(user.Id, user.Role, user.IsActive, cancellationToken);
        }

        if (draft.FullName != null)
        {
            user.FullName = TryRequireText(draft.FullName, "fullName");
        }

        if (nextDepartmentId != null)
        {
            user.DepartmentId = nextDepartmentId.Value;
        }

        user.Role = nextRole;
        user.IsActive = nextIsActive;

        await _users.SaveAsync(user, cancellationToken);

        await TryWriteAuditAsync(actorId, AuditAction.Update, user.Id, before, TryUserSummary(user), cancellationToken);

        return user;
    }

    /// <summary>
    /// Удаляет учётную запись (BR20, BR29/BR30).
    /// </summary>
    /// <param name="userId">Удаляемая учётная запись.</param>
    /// <param name="actorId">Администратор, который удаляет.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <remarks>Удаление последнего активного администратора запрещено — BusinessRuleException BR20.</remarks>
    public async Task DeleteUserAsync(Guid userId, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await GetKnownUserAsync(userId, cancellationToken);

        // BR20: удаление последнего активного администратора запрещено.
        // Правка аудита 3.2/3.3: мьютекс убран, проверка и удаление идут через await.
        await TryGuardLastActiveAdminAsync(user.Id, user.Role, user.IsActive, cancellationToken);

        await _users.DeleteAsync(userId, cancellationToken);

        await TryWriteAuditAsync(actorId, AuditAction.Delete, userId, TryUserSummary(user), null, cancellationToken);
    }

    // BR24: блокировка доступа при сохранении учётной записи и всех исторических данных.
    /// <summary>
    /// Блокирует доступ, сохраняя саму запись и все её исторические данные (BR24).
    /// </summary>
    /// <param name="userId">Деактивируемая учётная запись.</param>
    /// <param name="actorId">Администратор, который деактивирует.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Запись с заблокированным доступом.</returns>
    /// <remarks>Деактивация последнего активного администратора запрещена (BR20).</remarks>
    public async Task<User> DeactivateUserAsync(Guid userId, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await GetKnownUserAsync(userId, cancellationToken);

        // BR20: деактивация последнего активного администратора запрещена (BR24 для остальных).
        // Правка аудита 3.2/3.3: мьютекс убран, проверка и запись идут через await.
        await TryGuardLastActiveAdminAsync(user.Id, user.Role, user.IsActive, cancellationToken);

        user.IsActive = false;
        await _users.SaveAsync(user, cancellationToken);
        await TryWriteAuditAsync(
            actorId,
            AuditAction.Update,
            userId,
            "isActive=true",
            "isActive=false (BR24)",
            cancellationToken);

        return user;
    }

    // BR35: досрочная разблокировка — право администратора.
    /// <summary>
    /// Досрочная разблокировка учётной записи — право администратора (BR35).
    /// </summary>
    /// <param name="userId">Разблокируемая учётная запись.</param>
    /// <param name="actorId">Администратор, который разблокирует.</param>
    /// <param name="cancellationToken">Отменяет обращения к хранилищу.</param>
    /// <returns>Запись со снятой блокировкой и обнулённым счётчиком попыток.</returns>
    public async Task<User> UnlockUserAsync(Guid userId, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await GetKnownUserAsync(userId, cancellationToken);
        var before = "failedLoginAttempts=" + user.FailedLoginAttempts + ", locked=" + user.Locked;

        user.FailedLoginAttempts = 0;
        user.Locked = false;
        user.LockedUntil = null;

        await _users.SaveAsync(user, cancellationToken);
        await TryWriteAuditAsync(actorId, AuditAction.Update, userId, before, "unlocked (BR35)", cancellationToken);

        return user;
    }

    // BR20: в системе должен остаться хотя бы один активный администратор. Состояние передаётся
    // ДО правки. Правка аудита 3.2/3.3: метод полностью асинхронный (без блокирующих вызовов),
    // мьютекс учётных записей убран.
    private async Task TryGuardLastActiveAdminAsync(
        Guid targetId,
        UserRole role,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (role != UserRole.Admin || !isActive)
        {
            return;
        }

        var activeAdmins = 0;
        foreach (var candidate in await _users.GetAllAsync(cancellationToken))
        {
            if (candidate.Id != targetId && candidate.Role == UserRole.Admin && candidate.IsActive)
            {
                activeAdmins++;
            }
        }

        if (activeAdmins > 0)
        {
            return;
        }

        throw new BusinessRuleException(
            "BR20",
            "[RU] The action would leave the system with no active administrator (BR20).");
    }

    // Синтаксический контроль полей, который раньше делал контроллер через RequestParsers:
    // те же сообщения и те же коды в paramName (Application-слой не ссылается на Api-слой).
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

    // Enum name exactly as in Enums.cs (BR: never send the ordinal), same codes as the wire parser.
    private static UserRole TryParseUserRole(string? text)
    {
        TryRequireText(text, "role");

        var names = Enum.GetNames<UserRole>();
        foreach (var name in names)
        {
            if (string.Equals(name, text, StringComparison.Ordinal))
            {
                return Enum.Parse<UserRole>(name);
            }
        }

        throw new ArgumentException(
            "[RU] Field 'role' must be one of: " + string.Join(", ", names) + ".",
            paramName: "ROLE_INVALID");
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
            EntityType = "User",
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow,
        };

        await _auditLogs.SaveAsync(entry, cancellationToken);
    }

    private static string TryUserSummary(User user)
    {
        return "login=" + user.Login + ", role=" + user.Role + ", departmentId=" + user.DepartmentId
            + ", isActive=" + user.IsActive;
    }
}
