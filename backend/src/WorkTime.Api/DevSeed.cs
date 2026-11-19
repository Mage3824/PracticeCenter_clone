// Демо-сид dev-tier (in-memory) репозиториев для ручного запуска и smoke-проверки Stage 2.
//
// Без учётных записей приложение запускается, но ни один сценарий не проверяется: логин не
// находит пользователя, а защищённые эндпоинты не получают токен. Сид заполняет хранилище
// однократно при старте хоста, только в Development и только если оно пустое — интеграционные
// тесты сеют свои учётные записи сами и не зависят от этих.
//
// PasswordHash совпадает с паролем: хешер появляется на этапе 4 (AuthService.HashPasswordPlaceholder
// пока возвращает пароль как есть), поэтому сид работает ровно так же, как тестовые данные.
using WorkTime.Domain;

namespace WorkTime.Api;

/// <summary>Заполнение данных для локального запуска и smoke-проверки: подразделение и три роли.</summary>
public static class DevDataSeeder
{
    // Фиксированные id: smoke-проверка и Swagger UI ссылаются на них, а не на случайные Guid.
    /// <summary>Фиксированный id подразделения: на него ссылается smoke-проверка, а не случайные Guid.</summary>
    public static readonly Guid DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    /// <summary>Фиксированный id администратора.</summary>
    public static readonly Guid AdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    /// <summary>Фиксированный id руководителя.</summary>
    public static readonly Guid ManagerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    /// <summary>Фиксированный id сотрудника.</summary>
    public static readonly Guid EmployeeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    /// <summary>Логин администратора для локального запуска.</summary>
    public const string AdminLogin = "admin";
    /// <summary>Логин руководителя для локального запуска.</summary>
    public const string ManagerLogin = "manager";
    /// <summary>Логин сотрудника для локального запуска.</summary>
    public const string EmployeeLogin = "employee";

    /// <summary>Пароль администратора — только локальный запуск, не production.</summary>
    public const string AdminPassword = "admin-password-1";
    /// <summary>Пароль руководителя — только локальный запуск, не production.</summary>
    public const string ManagerPassword = "manager-password-1";
    /// <summary>Пароль сотрудника — только локальный запуск, не production.</summary>
    public const string EmployeePassword = "employee-password-1";

    // Заполняет department + три роли (Admin / Manager / Employee). Идемпотентно:
    // если в хранилище уже есть пользователи, ничего не добавляется.
    /// <summary>Заполняет подразделение и три роли; идемпотентно: если в хранилище уже есть пользователи, ничего не добавляется.</summary>
    public static async Task TrySeedAsync(
        IUserRepository users,
        IDepartmentRepository departments,
        CancellationToken cancellationToken)
    {
        if (users == null || departments == null)
        {
            return;
        }

        if ((await users.GetAllAsync(cancellationToken)).Any())
        {
            return;
        }

        await departments.SaveAsync(
            new Department { Id = DepartmentId, Name = "Плановый отдел", ParentId = null },
            cancellationToken);

        await users.SaveAsync(
            NewUser(AdminId, AdminLogin, AdminPassword, "Администратор системы", UserRole.Admin),
            cancellationToken);

        await users.SaveAsync(
            NewUser(ManagerId, ManagerLogin, ManagerPassword, "Руководитель отдела", UserRole.Manager),
            cancellationToken);

        await users.SaveAsync(
            NewUser(EmployeeId, EmployeeLogin, EmployeePassword, "Иван Петров", UserRole.Employee),
            cancellationToken);
    }

    private static User NewUser(Guid id, string login, string password, string fullName, UserRole role)
    {
        return new User
        {
            Id = id,
            Login = login,
            PasswordHash = PasswordHasher.TryHash(password),
            FullName = fullName,
            Role = role,
            DepartmentId = DepartmentId,
            IsActive = true,
            FailedLoginAttempts = 0,
            Locked = false,
            LockedUntil = null,
        };
    }
}
