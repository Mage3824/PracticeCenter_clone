// Maps thrown exceptions to RFC 7807 responses and enforces BR32 (8 hours of inactivity).
//
// WorkTimeExceptionHandler is registered with AddExceptionHandler<...>() and applied with
// UseExceptionHandler(), so it wraps the whole pipeline, including anything a controller throws:
//   BusinessRuleException          -> 409 Conflict          (code "BR1")
//   EntityNotFoundException        -> 404 Not Found         (+ entityType / entityId)
//   ArgumentException              -> 400 Bad Request       (request validation; ParamName is the code)
//   UnauthorizedAccessException    -> 401 Unauthorized      (+ WWW-Authenticate: Bearer)
//   any other Exception            -> 500 Internal Server Error (no internal message is leaked)
//
// The body is ProblemDetails (type / title / status / detail). The machine-readable keys of
// api-contracts.md - code, message, entityType, entityId, httpStatus - travel as ProblemDetails
// extensions, i.e. as top-level members of the same body, so the frontend still selects its text
// by code and never parses the human message.
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api;

/// <summary>Централизованный обработчик исключений: бизнес-правило становится 409 с кодом BR, отсутствующая сущность — 404, отказ доступа — 401 или 403, нечитаемый запрос — 400, неожиданный сбой — 500 со стабильным кодом. Конверт ошибки несёт code, message и httpStatus.</summary>
public class WorkTimeExceptionHandler : IExceptionHandler
{
    private readonly ILogger<WorkTimeExceptionHandler> _logger;

    /// <summary>Обработчик исключений: пишет необработанные сбои в встроенный журнал и отвечает конвертом ошибки.</summary>
    public WorkTimeExceptionHandler(ILogger<WorkTimeExceptionHandler> logger)
    {
        this._logger = logger;
    }

    /// <summary>Разбирает исключение запроса и пишет ответ; false означает, что исключение не распознано и уходит дальше.</summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        bool isUnauthorized = false;

        if (exception is BusinessRuleException businessRule)
        {
            problem = TryProblem(
                409,
                "https://errors.worktime/business-rule",
                "Business rule violated",
                businessRule.Code,
                exception.Message);
        }
        else if (exception is EntityNotFoundException missingEntity)
        {
            problem = TryProblem(
                404,
                "https://errors.worktime/entity-not-found",
                "Entity not found",
                "ENTITY_NOT_FOUND",
                exception.Message);
            problem.Extensions["entityType"] = missingEntity.EntityType;
            problem.Extensions["entityId"] = missingEntity.EntityId.ToString();
        }
        else if (exception is ArgumentException invalidRequest)
        {
            // ParamName carries the machine-readable code of the rejected request (DATE_INVALID,
            // ROLE_INVALID, ...), so the body keeps the same extension keys as before.
            problem = TryProblem(
                400,
                "https://errors.worktime/invalid-request",
                "Request rejected",
                invalidRequest.ParamName ?? "INVALID_REQUEST",
                exception.Message);
        }
        else if (exception is UnauthorizedAccessException unauthorized)
        {
            isUnauthorized = true;
            problem = TryProblem(
                401,
                "https://errors.worktime/unauthorized",
                "Authentication required",
                TryCodeFromMessage(unauthorized.Message),
                unauthorized.Message);
        }
        else
        {
            // Nothing about an unexpected failure crosses the wire except a stable code.
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            problem = TryProblem(
                500,
                "https://errors.worktime/internal-error",
                "The request could not be completed",
                "INTERNAL_ERROR",
                "[RU] The request could not be completed.");
        }

        httpContext.Response.StatusCode = problem.Status ?? 500;

        if (isUnauthorized)
        {
            httpContext.Response.Headers["WWW-Authenticate"] = "Bearer";
        }

        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static ProblemDetails TryProblem(
        int status,
        string type,
        string title,
        string code,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail,
        };

        problem.Extensions["code"] = code;
        problem.Extensions["message"] = detail;
        problem.Extensions["httpStatus"] = status;

        // The api-contracts.md envelope travels in the same body, so a frontend that reads
        // error.code keeps working next to the RFC 7807 members.
        problem.Extensions["error"] = ErrorEnvelope.TryEnvelope(code, detail, status);

        return problem;
    }

    // Code of an access refusal: when the message opens with "[CODE]", that code is used; anything
    // else maps to the generic UNAUTHORIZED. The "[RU]" prefix of the human messages in this
    // project is a language marker, not a code, so it is skipped before the lookup.
    private static string TryCodeFromMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "UNAUTHORIZED";
        }

        var text = message.StartsWith("[RU]", StringComparison.Ordinal)
            ? message.Substring(4)
            : message;

        if (text.Length < 2 || text[0] != '[')
        {
            return "UNAUTHORIZED";
        }

        var closing = text.IndexOf(']');
        if (closing <= 1)
        {
            return "UNAUTHORIZED";
        }

        var candidate = text.Substring(1, closing - 1);

        foreach (var ch in candidate)
        {
            if (!char.IsUpper(ch) && !char.IsDigit(ch) && ch != '_' && ch != '.' && ch != '-')
            {
                return "UNAUTHORIZED";
            }
        }

        return candidate;
    }
}

// Единственное место сборки envelope error{code,message,httpStatus} из api-contracts.md §1:
// exception handler, BR32 middleware и MVC-валидация используют только его.
/// <summary>Единственное место сборки конверта ошибки с code, message и httpStatus: обработчик исключений, middleware BR32 и MVC-валидация используют только его.</summary>
public static class ErrorEnvelope
{
    /// <summary>Собирает тело конверта ошибки для ответа.</summary>
    public static Dictionary<string, object> TryEnvelope(string code, string message, int httpStatus)
    {
        return new Dictionary<string, object>
        {
            ["code"] = code,
            ["message"] = message,
            ["httpStatus"] = httpStatus,
        };
    }
}

// In-memory record of the last activity per token subject (BR32). One instance per process,
// registered as a singleton; the expired entries are dropped by the middleware, so the dictionary
// does not grow without bound.
/// <summary>Память последней активности по субъекту токена (BR32): один экземпляр на процесс как singleton, просроченные записи убирает middleware, поэтому словарь не растёт бесконечно.</summary>
public class UserActivityTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new();
    private readonly TimeSpan _limit;
    private readonly object _pruneLock = new();
    private DateTime _lastPruneAt = DateTime.MinValue;

    /// <summary>Трекер активности: окно неактивности совпадает со сроком жизни токенов (BR32).</summary>
    public UserActivityTracker(JwtSettings settings)
    {
        // BR32: the same 8 hours the access token lives for.
        this._limit = TimeSpan.FromMinutes(settings.ExpiryMinutes);
    }

    /// <summary>Окно неактивности, после которого сессия считается просроченной (BR32).</summary>
    public TimeSpan InactivityLimit => this._limit;

    // True when more than the BR32 window passed since the last recorded activity.
    /// <summary>Истинно, когда с последней записанной активности прошло больше окна BR32.</summary>
    public bool TryIsExpired(string subject, DateTime now)
    {
        if (!_lastActivity.TryGetValue(subject, out var lastActivity))
        {
            return false;
        }

        return now - lastActivity > _limit;
    }

    /// <summary>Записывает момент активности субъекта: любой аутентифицированный запрос продлевает сессию.</summary>
    public void TryRecordActivity(string subject, DateTime now)
    {
        _lastActivity[subject] = now;

        TryPruneExpired(now);
    }

    private void TryPruneExpired(DateTime now)
    {
        lock (_pruneLock)
        {
            if (now - _lastPruneAt < _limit)
            {
                return;
            }

            _lastPruneAt = now;

            foreach (var pair in _lastActivity)
            {
                if (now - pair.Value > _limit)
                {
                    _lastActivity.TryRemove(pair.Key, out _);
                }
            }
        }
    }
}

// BR32 middleware: a token whose owner has been inactive for more than 8 hours is refused with 401
// (the client must log in again), whatever lifetime the token itself still has. Runs after
// authentication, so it reads the validated subject; runs before authorization, so the refusal is
// a session expiry rather than a role decision.
/// <summary>Middleware BR32: токен, чей владелец неактивен дольше восьми часов, отвергается 401 с требованием войти заново — независимо от оставшегося срока самого токена. Работает после аутентификации, поэтому читает проверенный субъект, и до авторизации, поэтому отказ — просрочка сессии, а не ролевое решение.</summary>
public class UserActivityTimeoutMiddleware : IMiddleware
{
    private readonly UserActivityTracker _tracker;
    private readonly ILogger<UserActivityTimeoutMiddleware> _logger;

    /// <summary>Middleware просрочки сессии: трекер активности и настройки JWT.</summary>
    public UserActivityTimeoutMiddleware(
        UserActivityTracker tracker,
        ILogger<UserActivityTimeoutMiddleware> logger)
    {
        this._tracker = tracker;
        this._logger = logger;
    }

    /// <summary>Проверяет окно BR32, отвергает 401 при просрочке и продлевает активность на любом аутентифицированном запросе.</summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var subject = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(subject))
        {
            await next(context);
            return;
        }

        var now = DateTime.UtcNow;

        if (_tracker.TryIsExpired(subject, now))
        {
            _logger.LogWarning("Session of user {UserId} expired after {Limit} of inactivity (BR32)",
                subject, _tracker.InactivityLimit);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Type = "https://errors.worktime/session-expired",
                Title = "Session expired",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "[RU] No activity for 8 hours - log in again (BR32).",
            };
            problem.Extensions["code"] = "SESSION_EXPIRED";
            problem.Extensions["message"] = problem.Detail;
            problem.Extensions["httpStatus"] = StatusCodes.Status401Unauthorized;
            problem.Extensions["error"] = ErrorEnvelope.TryEnvelope(
                "SESSION_EXPIRED", problem.Detail!, StatusCodes.Status401Unauthorized);

            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            return;
        }

        // BR32 (правка аудита): активность продлевается любым аутентифицированным
        // запросом — даже 400/409/404 через исключение показывает, что пользователь работает.
        try
        {
            await next(context);
        }
        finally
        {
            _tracker.TryRecordActivity(subject, DateTime.UtcNow);
        }
    }
}

// Request logging through the built-in ILogger (roadmap Stage 2: no Serilog in this stage).
/// <summary>Журналирование запросов через встроенный ILogger: Serilog на этом этапе не используется.</summary>
public class RequestLoggingMiddleware : IMiddleware
{
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>Middleware журналирования запросов.</summary>
    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
    {
        this._logger = logger;
    }

    /// <summary>Пишет строку журнала о запросе и передаёт запрос дальше.</summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startedAt = DateTime.UtcNow;

        await next(context);

        _logger.LogInformation(
            "{Method} {Path} -> {Status} in {ElapsedMs} ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            (DateTime.UtcNow - startedAt).TotalMilliseconds);
    }
}
