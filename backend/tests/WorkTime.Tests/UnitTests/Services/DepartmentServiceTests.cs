// xUnit-тесты для DepartmentService (src/WorkTime.Application/Services/DepartmentService.cs) —
// покрытия логики, перенесённой из DepartmentsController. Проверенные правила: дерево BR16/BR19
// (несуществующий родитель — 404; отдел не может быть собственным родителем — код в paramName),
// гейт удаления (отдел с активными сотрудниками не удаляется), BR29/BR30 (журнал событий).
namespace WorkTime.Tests.UnitTests.Services;

public class DepartmentServiceTests
{
    [Fact]
    public async Task CreateAsync_UnknownParentThrowsEntityNotFoundException()
    {
        // Arrange
        var ct = CancellationToken.None;

        var departments = new InMemoryDepartmentRepository();
        var users = new InMemoryUserRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new DepartmentService(departments, users, auditLogs);

        var unknownParentId = Guid.NewGuid();

        // Act
        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await service.CreateAsync(
                new CreateDepartmentDraft { Name = "Support", ParentId = unknownParentId },
                Guid.NewGuid(),
                ct));

        // Assert: 404 на отсутствующий родитель, ничего не создано и не записано в журнал.
        Assert.Equal(unknownParentId, ex.EntityId);
        Assert.Equal("Department", ex.EntityType);
        Assert.Empty(await departments.GetAllAsync(ct));
    }

    [Fact]
    public async Task UpdateAsync_DepartmentAsOwnParentThrowsArgumentExceptionWithSelfParentCode()
    {
        // Arrange
        var ct = CancellationToken.None;

        var departments = new InMemoryDepartmentRepository();
        var users = new InMemoryUserRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new DepartmentService(departments, users, auditLogs);

        var departmentId = Guid.NewGuid();
        await departments.SaveAsync(
            new Department { Id = departmentId, Name = "Support", ParentId = null },
            ct);

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.UpdateAsync(
                departmentId,
                new UpdateDepartmentDraft { ParentId = departmentId },
                Guid.NewGuid(),
                ct));

        // Assert: код 400 лежит в paramName; отдел остался прежним, журнал не тронут.
        Assert.Equal("SELF_PARENT", ex.ParamName);
        Assert.True(await departments.GetByIdAsync(departmentId, ct) != null);
        Assert.True((await departments.GetByIdAsync(departmentId, ct)).ParentId == null);
        Assert.Empty(await auditLogs.GetHistoryForEntityAsync(departmentId, ct));
    }

    [Fact]
    public async Task DeleteAsync_DepartmentWithActiveEmployeesThrowsBusinessRuleException()
    {
        // Arrange: отдел, в котором числится активный сотрудник.
        var ct = CancellationToken.None;

        var departments = new InMemoryDepartmentRepository();
        var users = new InMemoryUserRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new DepartmentService(departments, users, auditLogs);

        var departmentId = Guid.NewGuid();
        await departments.SaveAsync(
            new Department { Id = departmentId, Name = "Support", ParentId = null },
            ct);
        await users.SaveAsync(
            new User
            {
                Id = Guid.NewGuid(),
                Login = "ivan",
                FullName = "Ivan Petrov",
                Role = UserRole.Employee,
                DepartmentId = departmentId,
                IsActive = true,
            },
            ct);

        // Act
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await service.DeleteAsync(departmentId, Guid.NewGuid(), ct));

        // Assert: отдел не удалён, журнал не тронут.
        Assert.Equal("DEPARTMENT_HAS_EMPLOYEES", ex.Code);
        Assert.True(await departments.GetByIdAsync(departmentId, ct) != null);
        Assert.Empty(await auditLogs.GetHistoryForEntityAsync(departmentId, ct));
    }
}
