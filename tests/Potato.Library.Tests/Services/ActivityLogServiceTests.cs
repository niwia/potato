using FluentAssertions;
using Potato.Domain.ValueObjects;
using Potato.Library.Models;
using Potato.Library.Services;
using Xunit;

namespace Potato.Library.Tests.Services;

public class ActivityLogServiceTests : IDisposable
{
    private readonly string _testFile;

    public ActivityLogServiceTests()
    {
        _testFile = Path.Combine(Path.GetTempPath(), $"potato_act_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFile))
        {
            try { File.Delete(_testFile); } catch { }
        }
    }

    [Fact]
    public void RecordSuccess_ShouldAddActivityAndPersistToDisk()
    {
        var service = new ActivityLogService(_testFile);
        var appId = new AppId(603960);

        service.RecordSuccess(appId, "Star of Providence", 500UL * 1024 * 1024, TimeSpan.FromSeconds(20), "Depots: Verified • SLS: Synchronized");

        var activities = service.GetRecentActivities(10);
        activities.Should().HaveCount(1);
        activities[0].GameName.Should().Be("Star of Providence");
        activities[0].AppId.Should().Be(appId);
        activities[0].Status.Should().Be(ActivityStatus.Success);
        activities[0].StatusSummary.Should().Contain("500 MB in 20s");

        // Verify disk persistence by creating a new service instance pointing to the same file
        var reloadedService = new ActivityLogService(_testFile);
        var reloaded = reloadedService.GetRecentActivities(10);
        reloaded.Should().HaveCount(1);
        reloaded[0].GameName.Should().Be("Star of Providence");
    }

    [Fact]
    public void RecordFailure_ShouldRecordErrorMessage()
    {
        var service = new ActivityLogService(_testFile);
        var appId = new AppId(813230);

        service.RecordFailure(appId, "Animal Well", "Network timed out");

        var activities = service.GetRecentActivities(10);
        activities.Should().HaveCount(1);
        activities[0].Status.Should().Be(ActivityStatus.Failed);
        activities[0].ErrorMessage.Should().Be("Network timed out");
        activities[0].StatusSummary.Should().Contain("Failed • Network timed out");
    }
}
