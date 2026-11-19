// Gives a controller access to the current request without declaring framework implicit parameters:
// the stop signal (RequestAborted) every repository/service call takes, and the HttpContext whose
// claims carry the caller's identity. Registered as a scoped service in Program.cs.
namespace WorkTime.Api;

/// <summary>Доступ к текущему HTTP-контексту и сигналу остановки: личность несёт токен, контекст зарегистрирован как scoped service.</summary>
public class RequestContextProvider
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _accessor;

    /// <summary>Поставщик контекста поверх доступа к HTTP-контексту.</summary>
    public RequestContextProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <summary>Контекст текущего запроса; 500 когда контекста нет — вне HTTP-конвейера.</summary>
    public Microsoft.AspNetCore.Http.HttpContext TryContext()
    {
        var context = _accessor.HttpContext;

        if (context == null)
        {
            throw new ArgumentException(
                "[RU] The request context is unavailable for this call.",
                paramName: "NO_REQUEST_CONTEXT");
        }

        return context;
    }

    /// <summary>Сигнал остановки текущего запроса — передаётся в вызовы сервисов.</summary>
    public CancellationToken TryStopSignal()
    {
        return TryContext().RequestAborted;
    }
}
