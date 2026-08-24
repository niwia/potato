using FluentAssertions;
using Moq;
using Potato.Domain.ValueObjects;
using Potato.Pipeline.Models;
using Potato.Pipeline.Orchestrator;
using Potato.Queue.Events;
using Potato.Queue.Manager;
using Potato.Queue.Models;
using Xunit;

namespace Potato.Queue.Tests;

public class DownloadQueueManagerTests : IDisposable
{
    private readonly Mock<IInstallGameOrchestrator> _orchestratorMock;
    private readonly DownloadQueueManager _manager;

    public DownloadQueueManagerTests()
    {
        _orchestratorMock = new Mock<IInstallGameOrchestrator>();
        _manager = new DownloadQueueManager(_orchestratorMock.Object);
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    [Fact]
    public async Task Enqueue_ShouldStartJobAutomatically_AndCompleteSuccessfully()
    {
        var appId = new AppId(1003590);
        var request = new InstallRequest(appId, "/tmp/steam");

        var completedTcs = new TaskCompletionSource<InstallResult>();

        _orchestratorMock.Setup(o => o.InstallGameAsync(
                It.Is<InstallRequest>(r => r.AppId == appId),
                It.IsAny<IProgress<InstallProgressReport>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                return InstallResult.CreateSuccess(appId, "Tetris Effect", "TetrisEffect", "/tmp/acf", 1000);
            });

        QueueJobCompletedEventArgs? completedEvent = null;
        _manager.JobCompleted += (s, e) =>
        {
            completedEvent = e;
            completedTcs.TrySetResult(e.Result);
        };

        var job = _manager.Enqueue(request, "Tetris Effect");

        job.Should().NotBeNull();
        job.AppId.Should().Be(appId);

        var result = await completedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        result.Success.Should().BeTrue();
        completedEvent.Should().NotBeNull();
        completedEvent!.Job.Status.Should().Be(QueueJobStatus.Completed);
        completedEvent.Job.Result.Should().NotBeNull();

        var summary = _manager.GetSummary();
        summary.CompletedCount.Should().Be(1);
        summary.RunningCount.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrencyLimit_ShouldThrottleActiveDownloads()
    {
        _manager.MaxConcurrentDownloads = 1;

        var startTcs1 = new TaskCompletionSource();
        var blockTcs1 = new TaskCompletionSource();

        var startTcs2 = new TaskCompletionSource();

        _orchestratorMock.Setup(o => o.InstallGameAsync(
                It.Is<InstallRequest>(r => r.AppId == new AppId(1001)),
                It.IsAny<IProgress<InstallProgressReport>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                startTcs1.TrySetResult();
                await blockTcs1.Task;
                return InstallResult.CreateSuccess(new AppId(1001), "Game 1", "G1", "/tmp/1", 100);
            });

        _orchestratorMock.Setup(o => o.InstallGameAsync(
                It.Is<InstallRequest>(r => r.AppId == new AppId(1002)),
                It.IsAny<IProgress<InstallProgressReport>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                startTcs2.TrySetResult();
                return Task.FromResult(InstallResult.CreateSuccess(new AppId(1002), "Game 2", "G2", "/tmp/2", 200));
            });

        var job1 = _manager.Enqueue(new InstallRequest(new AppId(1001), "/tmp/1"), "Game 1");
        var job2 = _manager.Enqueue(new InstallRequest(new AppId(1002), "/tmp/2"), "Game 2");

        // Wait for Job 1 to start
        await startTcs1.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var summaryWhileRunning = _manager.GetSummary();
        summaryWhileRunning.RunningCount.Should().Be(1);
        summaryWhileRunning.QueuedCount.Should().Be(1);
        job2.Status.Should().Be(QueueJobStatus.Queued);

        // Release Job 1
        blockTcs1.TrySetResult();

        // Job 2 should now start automatically
        await startTcs2.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Task.Delay(100);
        var finalSummary = _manager.GetSummary();
        finalSummary.CompletedCount.Should().Be(2);
    }

    [Fact]
    public void MoveJobUpAndDown_ShouldReorderQueuedJobs()
    {
        _manager.PauseAll(); // Prevent immediate auto-start

        var job1 = _manager.Enqueue(new InstallRequest(new AppId(101), "/tmp/1"), "Job 1");
        var job2 = _manager.Enqueue(new InstallRequest(new AppId(102), "/tmp/2"), "Job 2");
        var job3 = _manager.Enqueue(new InstallRequest(new AppId(103), "/tmp/3"), "Job 3");

        // Initial order
        var jobs = _manager.GetAllJobs();
        jobs[0].Id.Should().Be(job1.Id);
        jobs[1].Id.Should().Be(job2.Id);
        jobs[2].Id.Should().Be(job3.Id);

        // Move Job 3 Up
        bool movedUp = _manager.MoveJobUp(job3.Id);
        movedUp.Should().BeTrue();

        jobs = _manager.GetAllJobs();
        jobs[1].Id.Should().Be(job3.Id);
        jobs[2].Id.Should().Be(job2.Id);

        // Move Job 1 Down
        bool movedDown = _manager.MoveJobDown(job1.Id);
        movedDown.Should().BeTrue();

        jobs = _manager.GetAllJobs();
        jobs[0].Id.Should().Be(job3.Id);
        jobs[1].Id.Should().Be(job1.Id);
    }

    [Fact]
    public async Task CancelJob_ShouldCancelExecutionAndMarkCancelled()
    {
        var startTcs = new TaskCompletionSource();
        var cancelTcs = new TaskCompletionSource();

        _orchestratorMock.Setup(o => o.InstallGameAsync(
                It.IsAny<InstallRequest>(),
                It.IsAny<IProgress<InstallProgressReport>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (InstallRequest r, IProgress<InstallProgressReport>? p, CancellationToken ct) =>
            {
                startTcs.TrySetResult();
                try
                {
                    await Task.Delay(5000, ct);
                    return InstallResult.CreateSuccess(r.AppId, "Game", "G", "/tmp/1", 100);
                }
                catch (OperationCanceledException)
                {
                    cancelTcs.TrySetResult();
                    throw;
                }
            });

        var job = _manager.Enqueue(new InstallRequest(new AppId(1001), "/tmp/1"), "Game 1");

        await startTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        bool cancelled = _manager.CancelJob(job.Id);
        cancelled.Should().BeTrue();

        await cancelTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        job.Status.Should().Be(QueueJobStatus.Cancelled);
        job.IsTerminal.Should().BeTrue();
    }
}
