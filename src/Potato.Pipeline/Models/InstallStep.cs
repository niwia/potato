namespace Potato.Pipeline.Models;

/// <summary>
/// Execution phases of the game installation pipeline.
/// </summary>
public enum InstallStep
{
    ResolvingMetadata = 1,
    ResolvingKeys = 2,
    ResolvingManifests = 3,
    DownloadingDepots = 4,
    FinalizingAcf = 5,
    Completed = 6,
    Failed = 7
}
