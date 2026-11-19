// Пользователи: CRUD, деактивация (BR24), разблокировка (BR35). Бизнес-логика и записи в журнал
// событий — в UserService (этап: контроллер не зависит от репозиториев); здесь только контекст
// запроса, личность субъекта токена и сборка ответа.
// Права по BR19: запись — только администратор; чтение — руководитель и администратор.
// Ответы никогда не содержат PasswordHash (BR34).
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api;

/// <summary>Учётные записи сотрудников: чтение — руководителю и администратору, изменение — администратору (BR19, BR24, BR34, BR35).</summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    private readonly RequestContextProvider _requestContext;

    /// <summary>Контроллер учётных записей: сервис сотрудников и контекст запроса.</summary>
    public UsersController(UserService userService, RequestContextProvider requestContext)
    {
        _userService = userService;
        _requestContext = requestContext;
    }

    /// <summary>Список сотрудников по имени; includeInactive добавляет заблокированные учётные записи (BR24). 200 со списком UserWire, пустой список когда совпадений нет.</summary>
    [HttpGet("")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserWire[]))]
    public async Task<UserWire[]> TryListUsers([FromQuery] string? name, [FromQuery] bool includeInactive)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var found = await _userService.ListUsersAsync(name, includeInactive, cancellationToken);
        return ManagementWireConversion.TryFromUsers(found);
    }

    /// <summary>Учётная запись по id. 200 с UserWire, 404 когда id неизвестен.</summary>
    [HttpGet("{userId}")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserWire))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UserWire> TryGetUser(Guid userId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var user = await _userService.GetKnownUserAsync(userId, cancellationToken);
        return ManagementWireConversion.TryFromUser(user);
    }

    /// <summary>Создаёт учётную запись — администратор (BR34: пароль приходит только в запросе и никогда не возвращается). 200 с UserWire, 400 на пустые поля или нечитаемый токен роли, 404 на неизвестное подразделение, 409 на занятый логин или нарушение BR.</summary>
    [HttpPost("")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<UserWire> TryCreateUser(CreateUserRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var user = await _userService.CreateUserAsync(
            new CreateUserDraft
            {
                Login = payload.Login,
                FullName = payload.FullName,
                Password = payload.Password,
                Role = payload.Role,
                DepartmentId = payload.DepartmentId,
            },
            actorId,
            cancellationToken);

        return ManagementWireConversion.TryFromUser(user);
    }

    /// <summary>Меняет имя, роль, подразделение и доступ учётной записи — администратор (BR24: снятие доступа без удаления записи). 200 с UserWire, 400 на нечитаемый токен роли, 404 на неизвестный id или подразделение, 409 на нарушение бизнес-правила.</summary>
    [HttpPut("{userId}")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<UserWire> TryUpdateUser(Guid userId, UpdateUserRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var user = await _userService.UpdateUserAsync(
            userId,
            new UpdateUserDraft
            {
                Role = payload.Role,
                IsActive = payload.IsActive,
                DepartmentId = payload.DepartmentId,
                FullName = payload.FullName,
            },
            actorId,
            cancellationToken);

        return ManagementWireConversion.TryFromUser(user);
    }

    /// <summary>Удаляет учётную запись — администратор. 200 без тела, 404 на неизвестный id, 409 когда удаление нарушает бизнес-правило.</summary>
    [HttpDelete("{userId}")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task TryDeleteUser(Guid userId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        await _userService.DeleteUserAsync(userId, actorId, cancellationToken);
    }

    // BR24: блокировка доступа при сохранении учётной записи и всех исторических данных.
    /// <summary>Блокирует доступ учётной записи, сохраняя её саму и всю историю (BR24). 200 с UserWire, 404 на неизвестный id, 409 на нарушение бизнес-правила.</summary>
    [HttpPost("{userId}/deactivate")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserWire))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<UserWire> TryDeactivateUser(Guid userId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var user = await _userService.DeactivateUserAsync(userId, actorId, cancellationToken);
        return ManagementWireConversion.TryFromUser(user);
    }

    // BR35: досрочная разблокировка — право администратора.
    /// <summary>Досрочная разблокировка учётной записи — право администратора (BR35). 200 с UserWire, 404 на неизвестный id.</summary>
    [HttpPost("{userId}/unlock")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserWire))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UserWire> TryUnlockUser(Guid userId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var user = await _userService.UnlockUserAsync(userId, actorId, cancellationToken);
        return ManagementWireConversion.TryFromUser(user);
    }
}

