namespace WindowsNotch.App.Services;

public sealed record CopyResult(bool Success, string Message, string? DestinationFolder, int CopiedCount);
