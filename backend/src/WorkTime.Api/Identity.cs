// The identity of the caller, read from the access token and never from a body (api-contracts.md §6:
// "the client sends criteria - id / date / period / department - never a list of ids", and Stage 2
// fixes employeeId / currentUserId / editorId / actorId to come from the token subject).
using System.Security.Claims;
using WorkTime.Domain;

namespace WorkTime.Api;

/// <summary>Чтение личности вызывающего из проверенного токена: субъект и роль. Токен без субъекта или субъект не Guid — отвергнутый запрос, а не бизнес-правило.</summary>
public class RequestIdentity
{
    // ClaimTypes.NameIdentifier -> the Guid of the authenticated User. A token that carries no
    // subject, or a subject that is not a Guid, is a rejected request rather than a business rule.
    /// <summary>Guid пользователя из claim субъекта; 400 когда субъекта нет или это не Guid.</summary>
    public static Guid TrySubjectUserId(HttpContext context)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (subject == null)
        {
            throw new ArgumentException(
                "[RU] The access token carries no subject claim.",
                paramName: "IDENTITY_MISSING");
        }

        if (!Guid.TryParse(subject, out var userId))
        {
            throw new ArgumentException(
                "[RU] The access token subject is not a user id.",
                paramName: "IDENTITY_INVALID");
        }

        return userId;
    }

    // ClaimTypes.Role -> UserRole, for the checks that need the role in code rather than through an
    // authorization policy (the same token both authorizes and identifies the caller).
    /// <summary>Роль из claim роли — для проверок в коде, а не через политику авторизации.</summary>
    public static UserRole TryRole(HttpContext context)
    {
        var roleName = context.User.FindFirstValue(ClaimTypes.Role);

        if (roleName != null)
        {
            foreach (var role in Enum.GetValues<UserRole>())
            {
                if (role.ToString() == roleName)
                {
                    return role;
                }
            }
        }

        throw new ArgumentException(
            "[RU] The access token carries no role claim.",
            paramName: "ROLE_MISSING");
    }

    // Stage-2 identity gap of api-contracts.md §8: a service such as StartWorkDayAsync does not
    // resolve the user itself, so every controller checks that the token subject is a known User
    // before any business rule runs - an unknown subject is a 404, never a created record.
    // Существование субъекта проверяет UserService (Application-слой): хелпер остаётся в Api,
    // но больше не обращается к репозиторию пользователей.
    /// <summary>Проверяет, что субъект токена — известная учётная запись, иначе 404: сервис, который сам не читает пользователя, не должен создать запись «ни для кого».</summary>
    public static async Task<User> TryRequireKnownUserAsync(
        UserService userService,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await userService.GetKnownUserAsync(userId, cancellationToken);
    }
}
