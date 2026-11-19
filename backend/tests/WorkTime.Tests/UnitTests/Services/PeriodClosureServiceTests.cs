// xUnit-тесты для PeriodClosureService (src/WorkTime.Application/Services/PeriodClosureService.cs)
// — покрытия логики, перенесённой из AdminController. Проверенные правила: BR23.1 (закрытие
// расчётного периода), BR23.2 (вскрытие закрытого периода требует причину и пишется в журнал как
// «Экстренная корректировка»), BR29/BR30 (журнал событий). Порядок для вскрытия: пустая причина
// разбирается раньше существования id — 400 идёт до 404.
namespace WorkTime.Tests.UnitTests.Services;

public class PeriodClosureServiceTests
{
    [Fact]
    public async Task ClosePeriodAsync_OpenPeriod_SetsClosedAndLogsAudit()
    {
        // Arrange
        var ct = CancellationToken.None;

        var periodClosures = new InMemoryPeriodClosureRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new PeriodClosureService(periodClosures, new AuditLogService(auditLogs));

        var actorId = Guid.NewGuid();
        var closureId = Guid.NewGuid();
        await periodClosures.SaveAsync(
            new PeriodClosure
            {
                Id = closureId,
                DepartmentId = Guid.NewGuid(),
                Year = 2024,
                Month = 3,
                IsClosed = false,
            },
            ct);

        // Act
        var closed = await service.ClosePeriodAsync(closureId, actorId, ct);

        // Assert
        Assert.True(closed.IsClosed);
        Assert.Equal(actorId, closed.ClosedBy);
        Assert.True(closed.ClosedAt != null);

        var stored = await periodClosures.GetByIdAsync(closureId, ct);
        Assert.True(stored != null && stored.IsClosed);

        var history = new List<AuditLog>(await auditLogs.GetHistoryForEntityAsync(closureId, ct));
        Assert.Single(history);
        Assert.True(history[0].Action == AuditAction.Update);
        Assert.Equal("PeriodClosure", history[0].EntityType);
        Assert.Equal(actorId, history[0].UserId);
        Assert.Contains("BR23.1", history[0].NewValue ?? string.Empty);
    }

    [Fact]
    public async Task ReopenPeriodAsync_ClosedPeriod_StoresReasonAndLogsEmergencyCorrection()
    {
        // Arrange
        var ct = CancellationToken.None;

        var periodClosures = new InMemoryPeriodClosureRepository();
        var auditLogs = new InMemoryAuditLogRepository();
        var service = new PeriodClosureService(periodClosures, new AuditLogService(auditLogs));

        var actorId = Guid.NewGuid();
        var closureId = Guid.NewGuid();
        await periodClosures.SaveAsync(
            new PeriodClosure
            {
                Id = closureId,
                DepartmentId = Guid.NewGuid(),
                Year = 2024,
                Month = 3,
                IsClosed = true,
                ClosedAt = DateTime.UtcNow,
                ClosedBy = actorId,
            },
            ct);

        // Act
        var reopened = await service.ReopenPeriodAsync(closureId, "act #42", actorId, ct);

        // Assert
        Assert.False(reopened.IsClosed);
        Assert.Equal("act #42", reopened.ReopenReason);
        Assert.Equal(actorId, reopened.ReopenedByUserId);

        var history = new List<AuditLog>(await auditLogs.GetHistoryForEntityAsync(closureId, ct));
        Assert.Single(history);
        Assert.Contains("Emergency correction", history[0].NewValue ?? string.Empty);
        Assert.Contains("act #42", history[0].NewValue ?? string.Empty);

        // Пустая причина — 400 раньше, чем проверка существования id.
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ReopenPeriodAsync(Guid.NewGuid(), " ", Guid.NewGuid(), ct));
        Assert.Equal("FIELD_MISSING", ex.ParamName);
    }
}
