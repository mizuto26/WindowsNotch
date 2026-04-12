using System.Diagnostics;
using Microsoft.Win32;

namespace WindowsNotch.App.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "WindowsNotch";

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var registeredValue = runKey?.GetValue(AppValueName) as string;
        return !string.IsNullOrWhiteSpace(registeredValue);
    }

    public void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Startup registry key could not be opened.");

        if (!enabled)
        {
            runKey.DeleteValue(AppValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("The current executable path could not be resolved.");
        }

        runKey.SetValue(AppValueName, $"\"{executablePath}\"");
    }
}
