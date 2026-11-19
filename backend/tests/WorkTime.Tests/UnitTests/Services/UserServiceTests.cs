// xUnit-тесты для UserService (src/WorkTime.Application/Services/UserService.cs) — покрытия
// логики, перенесённой из UsersController. Проверенные правила: BR34 (пароль пишется хешем),
// BR20 (в системе должен остаться хотя бы один активный администратор), BR35 (досрочная
// разблокировка снимает блокировку и обнуляет счётчик попыток), BR29/BR30 (правка идёт в журнал),
// плюс синтаксический контроль полей: код ошибки лежит в paramName (400 на Wire-уровне).
namespace WorkTime.Tests.UnitTests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task CreateUserAsync_ValidDraft_StoresHashedPasswordAndLogsCreate()
    {
        // Arrange
        var ct = CancellationToken.None;

        var users = new InMemoryUserRepository();
        var departments = new InMemoryDepartmentRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new UserService(users, departments, auditLogs);

        var departmentId = Guid.NewGuid();
        await departments.SaveAsync(
            new Department { Id = departmentId, Name = "Support" },
            ct);

        // Act
        var created = await service.CreateUserAsync(
            new CreateUserDraft
            {
                Login = "ivan",
                FullName = "Ivan Petrov",
                Password = "plain-text-password",
                Role = "Employee",
                DepartmentId = departmentId,
            },
            Guid.NewGuid(),
            ct);

        // Assert
        Assert.True(created.IsActive);
        Assert.True(created.Role == UserRole.Employee);
        Assert.Equal(departmentId, created.DepartmentId);

        var stored = await users.GetByLoginAsync("ivan", ct);
        Assert.True(stored != null);
        Assert.True(stored.PasswordHash != "plain-text-password");
        Assert.False(string.IsNullOrEmpty(stored.PasswordHash));

        var history = new List<AuditLog>(await auditLogs.GetHistoryForEntityAsync(created.Id, ct));
        Assert.Single(history);
        Assert.True(history[0].Action == AuditAction.Create);
        Assert.Equal("User", history[0].EntityType);
        Assert.Equal(created.Id, history[0].EntityId);
    }

    [Fact]
    public async Task CreateUserAsync_MissingLoginThrowsArgumentExceptionWithFieldMissingCode()
    {
        // Arrange
        var ct = CancellationToken.None;

        var users = new InMemoryUserRepository();
        var departments = new InMemoryDepartmentRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new UserService(users, departments, auditLogs);

        var departmentId = Guid.NewGuid();
        await departments.SaveAsync(
            new Department { Id = departmentId, Name = "Support" },
            ct);

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.CreateUserAsync(
                new CreateUserDraft
                {
                    Login = " ",
                    FullName = "Ivan Petrov",
                    Password = "secret",
                    Role = "Employee",
                    DepartmentId = departmentId,
                },
                Guid.NewGuid(),
                ct));

        // Assert: код 400 лежит в paramName, сообщение называет то же поле.
        Assert.Equal("FIELD_MISSING", ex.ParamName);
        Assert.Contains("login", ex.Message);
        Assert.Empty(await users.GetAllAsync(ct));
    }

    [Fact]
    public async Task DeleteUserAsync_LastActiveAdminThrowsBusinessRuleException()
    {
        // Arrange: единственный активный администратор и один сотрудник.
        var ct = CancellationToken.None;

        var users = new InMemoryUserRepository();
        var departments = new InMemoryDepartmentRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new UserService(users, departments, auditLogs);

        var departmentId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        await users.SaveAsync(
            new User
            {
                Id = adminId,
                Login = "admin",
                FullName = "Admin",
                Role = UserRole.Admin,
                DepartmentId = departmentId,
                IsActive = true,
            },
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
            await service.DeleteUserAsync(adminId, Guid.NewGuid(), ct));

        // Assert: администратор остался в хранилище, журнал не тронут.
        Assert.Equal("BR20", ex.Code);
        Assert.True(await users.GetByIdAsync(adminId, ct) != null);
        Assert.Empty(await auditLogs.GetHistoryForEntityAsync(adminId, ct));
    }

    [Fact]
    public async Task UnlockUserAsync_LockedAccountClearsLockAndLogsUnlock()
    {
        // Arrange: учётная запись заблокирована после пяти неверных попыток.
        var ct = CancellationToken.None;

        var users = new InMemoryUserRepository();
        var departments = new InMemoryDepartmentRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new UserService(users, departments, auditLogs);

        var userId = Guid.NewGuid();
        await users.SaveAsync(
            new User
            {
                Id = userId,
                Login = "ivan",
                FullName = "Ivan Petrov",
                Role = UserRole.Manager,
                DepartmentId = Guid.NewGuid(),
                IsActive = true,
                FailedLoginAttempts = 5,
                Locked = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(15),
            },
            ct);

        // Act
        var unlocked = await service.UnlockUserAsync(userId, Guid.NewGuid(), ct);

        // Assert
        Assert.False(unlocked.Locked);
        Assert.True(unlocked.LockedUntil == null);
        Assert.Equal(0, unlocked.FailedLoginAttempts);

        var history = new List<AuditLog>(await auditLogs.GetHistoryForEntityAsync(userId, ct));
        Assert.Single(history);
        Assert.True(history[0].Action == AuditAction.Update);
        Assert.Contains("BR35", history[0].NewValue ?? string.Empty);
    }
}
