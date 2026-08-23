using Potato.Pipeline.Models;

namespace Potato.Pipeline.Orchestrator;

/// <summary>
/// Orchestrates the complete end-to-end installation pipeline for a Steam game.
/// </summary>
public interface IInstallGameOrchestrator
{
    Task<InstallResult> InstallGameAsync(
        InstallRequest request,
        IProgress<InstallProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}
