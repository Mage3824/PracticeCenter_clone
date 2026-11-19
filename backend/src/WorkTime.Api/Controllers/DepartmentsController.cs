// Отделы: CRUD (BR19 — настройка системы belongs to the administrator; BR23.1 — закрытие
// расчётного периода настраивается по отделу, поэтому дерево отделов доступно руководителю на чтение).
// Контроллер держит только сервис (DepartmentService), контекст запроса и личность субъекта
// (UserService): репозиториев здесь нет, правила дерева и запись в журнал живут в Application-слое.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api;

/// <summary>Дерево подразделений: чтение — руководителю и администратору, изменение — администратору (BR19, BR23.1).</summary>
[ApiController]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly DepartmentService _departmentService;

    private readonly UserService _userService;

    private readonly RequestContextProvider _requestContext;

    /// <summary>Контроллер подразделений: сервис дерева и контекст запроса.</summary>
    public DepartmentsController(
        DepartmentService departmentService,
        UserService userService,
        RequestContextProvider requestContext)
    {
        _departmentService = departmentService;
        _userService = userService;
        _requestContext = requestContext;
    }

    /// <summary>Список подразделений. 200 со списком DepartmentWire, пустой список когда подразделений нет.</summary>
    [HttpGet("")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DepartmentWire[]))]
    public async Task<DepartmentWire[]> TryListDepartments()
    {
        var cancellationToken = _requestContext.TryStopSignal();
        return ManagementWireConversion.TryFromDepartments(
            await _departmentService.ListAsync(cancellationToken));
    }

    /// <summary>Подразделение по id. 200 с DepartmentWire, 404 когда id неизвестен.</summary>
    [HttpGet("{departmentId}")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DepartmentWire))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DepartmentWire> TryGetDepartment(Guid departmentId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        return ManagementWireConversion.TryFromDepartment(
            await _departmentService.GetAsync(departmentId, cancellationToken));
    }

    /// <summary>Дочерние узлы подразделения. 200 со списком DepartmentWire, 404 когда родитель неизвестен.</summary>
    [HttpGet("{departmentId}/children")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DepartmentWire[]))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DepartmentWire[]> TryListChildren(Guid departmentId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        return ManagementWireConversion.TryFromDepartments(
            await _departmentService.ListChildrenAsync(departmentId, cancellationToken));
    }

    /// <summary>Создаёт подразделение — администратор. 200 с DepartmentWire, 400 на пустое имя или нечитаемый parentId, 404 на неизвестный родитель.</summary>
    [HttpPost("")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DepartmentWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DepartmentWire> TryCreateDepartment(CreateDepartmentRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var department = await _departmentService.CreateAsync(
            new CreateDepartmentDraft { Name = payload.Name, ParentId = payload.ParentId },
            actorId,
            cancellationToken);

        return ManagementWireConversion.TryFromDepartment(department);
    }

    /// <summary>Меняет имя или родителя подразделения — администратор. 200 с DepartmentWire, 400 на нечитаемые поля, 404 на неизвестный id или новый родитель.</summary>
    [HttpPut("{departmentId}")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DepartmentWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DepartmentWire> TryUpdateDepartment(Guid departmentId, UpdateDepartmentRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        var department = await _departmentService.UpdateAsync(
            departmentId,
            new UpdateDepartmentDraft { Name = payload.Name, ParentId = payload.ParentId },
            actorId,
            cancellationToken);

        return ManagementWireConversion.TryFromDepartment(department);
    }

    /// <summary>Удаляет подразделение — администратор. 200 без тела, 404 на неизвестный id, 409 когда у подразделения есть дети или учётные записи.</summary>
    [HttpDelete("{departmentId}")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task TryDeleteDepartment(Guid departmentId)
    {
        var cancellationToken = _requestContext.TryStopSignal();
        var actorId = RequestIdentity.TrySubjectUserId(_requestContext.TryContext());
        await RequestIdentity.TryRequireKnownUserAsync(_userService, actorId, cancellationToken);

        await _departmentService.DeleteAsync(departmentId, actorId, cancellationToken);
    }
}

