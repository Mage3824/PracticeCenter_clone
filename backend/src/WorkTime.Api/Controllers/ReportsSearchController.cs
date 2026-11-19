
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WorkTime.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsSearchController : ControllerBase
{
    private readonly ReportSearchService _searchService;
    private readonly UserService _userService;
    private readonly RequestContextProvider _requestContext;

    public ReportsSearchController(
        ReportSearchService searchService,
        UserService userService,
        RequestContextProvider requestContext)
    {
        _searchService = searchService;
        _userService = userService;
        _requestContext = requestContext;
    }

    // Поиск отчётов по выбранным параметрам
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? text,
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? departmentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var cancellationToken =
            _requestContext.TryStopSignal();

        var currentUserId = RequestIdentity.TrySubjectUserId(
            _requestContext.TryContext());

        await RequestIdentity.TryRequireKnownUserAsync(
        _userService, currentUserId, cancellationToken);

        // Конечная дата не должна быть раньше начальной
        if (from.HasValue && to.HasValue &&
            from.Value.Date > to.Value.Date)
        {
            return BadRequest(new
            {
                code = "INVALID_DATE_RANGE",
                message = "Начальная дата больше конечной."
            });
        }

        var result = await _searchService.SearchAsync(
            currentUserId,
            text,
            employeeId,
            departmentId,
            from,
            to,
            cancellationToken);

        return Ok(result);
    }
}
