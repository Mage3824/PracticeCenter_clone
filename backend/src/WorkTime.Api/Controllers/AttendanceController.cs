using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkTime.Domain;

namespace WorkTime.Api;

/// <summary>Presence summary for managers and admins. Server determines manager's department.</summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly AttendanceSummaryService _summary;
    private readonly IUserRepository _users;
    private readonly RequestContextProvider _context;

    public AttendanceController(AttendanceSummaryService summary, IUserRepository users, RequestContextProvider context)
    {
        _summary = summary;
        _users = users;
        _context = context;
    }

    [HttpGet("summary")]
    [Authorize(Policy = WorkTimeAuthorizationPolicies.ManagerOrAdmin)]
    public async Task<List<AttendanceRowDto>> GetSummary([FromQuery] DateTime? date,
        [FromQuery] Guid? departmentId)
    {
        var cancellationToken = _context.TryStopSignal();
        var callerId = RequestIdentity.TrySubjectUserId(_context.TryContext());
        var caller = await _users.GetByIdAsync(callerId, cancellationToken)
            ?? throw new EntityNotFoundException(callerId, "User");
        // A manager may view their own department only. Admin can request any department.
        var requestedDepartment = caller.Role == UserRole.Manager
            ? caller.DepartmentId
            : departmentId ?? Guid.Empty;
        return await _summary.GetSummaryAsync((date ?? DateTime.Now).Date,
            requestedDepartment, cancellationToken);
    }
}
