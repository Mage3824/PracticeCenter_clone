// Endpoint 1 of api-contracts.md: POST /api/auth/login.
//
// Login is the one endpoint that runs without a token - it is how a token is obtained. A failed login
// is data, never an error (BR35 lock, BR36 CAPTCHA), so the response stays 200 with AuthResult flags;
// only on success does the Stage-2 addition add accessToken / expiresIn (a JWT whose subject is the
// User's Guid and whose role claim drives the authorization policies).
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WorkTime.Api;

/// <summary>Вход в систему — единственный эндпоинт, работающий без токена. Неудачный вход это данные, а не ошибка: блокировка (BR35), обратный отсчёт попыток и требование CAPTCHA (BR36) возвращаются флагами в ответе 200.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    private readonly AuthService _authService;

    private readonly TokenService _tokenService;

    private readonly RequestContextProvider _requestContext;

    private readonly UserActivityTracker _activity;

    /// <summary>Контроллер входа: сервис аутентификации и выпуск токена доступа.</summary>
    public AuthController(
        AuthService authService,
        TokenService tokenService,
        RequestContextProvider requestContext,
        UserActivityTracker activity)
    {
        _authService = authService;
        _tokenService = tokenService;
        _requestContext = requestContext;
        _activity = activity;
    }

    /// <summary>Проверяет логин и пароль. Ответ 200 с LoginWire: флаги блокировки и CAPTCHA вместо ошибок; 400 на нечитаемое тело; 429 на превышение лимита входов. При успехе ответ несёт идентификатор учётной записи, accessToken и expiresIn — субъект токена это Guid пользователя.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    // Аудит 2.2: ограничен только вход — policy "login-rate-limit" из Program.cs.
    [EnableRateLimiting(Program.LoginRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginWire))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<LoginWire> TryLogin(LoginRequest payload)
    {
        var cancellationToken = _requestContext.TryStopSignal();

        RequestParsers.TryRequireText(payload.Login, "login");
        RequestParsers.TryRequireText(payload.Password, "password");

        // AuthService never throws for a bad login: the lock countdown and the CAPTCHA prompt come
        // back as flags (BR35/BR36).
        var result = await _authService.LoginAsync(payload.Login!, payload.Password!, cancellationToken);

        if (!result.IsSuccess)
        {
            return WireConversion.TryFromAuthResult(result, null, 0);
        }

        // A successful login carries the identity of the account on AuthResult, because that Guid is
        // what the access token names as its subject (BR35/BR36 semantics stay in AuthService). A
        // success without identity -> ENTITY_NOT_FOUND / "User" (404).
        if (result.UserId == Guid.Empty)
        {
            throw new EntityNotFoundException(Guid.Empty, "User");
        }

        // BR32 (правка аудита): успешный вход восстанавливает baseline активности,
        // иначе просроченная запись блокировала бы сессию навсегда.
        _activity.TryRecordActivity(result.UserId.ToString(), DateTime.UtcNow);

        var accessToken = await _tokenService.TryIssueAccessToken(result.UserId, result.Role, payload.Login!, cancellationToken);
        var expiresIn = _tokenService.TryExpiresInSeconds();

        return WireConversion.TryFromAuthResult(result, accessToken, expiresIn);
    }
}

