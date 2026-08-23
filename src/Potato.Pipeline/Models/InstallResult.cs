using Potato.Domain.ValueObjects;

namespace Potato.Pipeline.Models;

/// <summary>
/// Final result of the complete installation pipeline.
/// </summary>
public sealed record InstallResult
{
    public bool Success { get; init; }
    public AppId AppId { get; init; }
    public string? GameName { get; init; }
    public string? InstallDir { get; init; }
    public string? AcfPath { get; init; }
    public long TotalBytesOnDisk { get; init; }
    public string? ErrorMessage { get; init; }

    public InstallResult(
        bool success,
        AppId appId,
        string? gameName = null,
        string? installDir = null,
        string? acfPath = null,
        long totalBytesOnDisk = 0,
        string? errorMessage = null)
    {
        Success = success;
        AppId = appId;
        GameName = gameName;
        InstallDir = installDir;
        AcfPath = acfPath;
        TotalBytesOnDisk = totalBytesOnDisk;
        ErrorMessage = errorMessage;
    }

    public static InstallResult CreateSuccess(
        AppId appId,
        string gameName,
        string installDir,
        string acfPath,
        long totalBytesOnDisk) =>
        new(true, appId, gameName, installDir, acfPath, totalBytesOnDisk);

    public static InstallResult CreateFailure(
        AppId appId,
        string errorMessage) =>
        new(false, appId, errorMessage: errorMessage);
}
