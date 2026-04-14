using System.IO;
using System.Windows;
using System.Windows.Threading;
using WindowsNotch.App.Services;

namespace WindowsNotch.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = WriteCrashLog(e.Exception);
        MessageBox.Show(
            $"WindowsNotch crashed.\n\n{e.Exception}\n\nLog:\n{logPath}",
            "WindowsNotch",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(-1);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception.");
        var logPath = WriteCrashLog(exception);

        MessageBox.Show(
            $"WindowsNotch crashed.\n\n{exception}\n\nLog:\n{logPath}",
            "WindowsNotch",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string WriteCrashLog(Exception exception)
    {
        Directory.CreateDirectory(AppPaths.LogRoot);

        var logPath = Path.Combine(AppPaths.LogRoot, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var logText =
            $"Timestamp: {DateTime.Now:O}{Environment.NewLine}" +
            $"OS: {Environment.OSVersion}{Environment.NewLine}" +
            $"Process: {Environment.ProcessPath}{Environment.NewLine}" +
            Environment.NewLine +
            exception;

        File.WriteAllText(logPath, logText);
        return logPath;
    }
}
