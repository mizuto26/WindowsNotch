using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WindowsNotch.App.Services;

namespace WindowsNotch.App;

public partial class App : Application
{
    private int _isHandlingFatalException;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalError(e.Exception, shutdownAfterClose: true);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception.");
        ShowFatalError(exception, shutdownAfterClose: e.IsTerminating);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowFatalError(e.Exception, shutdownAfterClose: false);
        e.SetObserved();
    }

    private void ShowFatalError(Exception exception, bool shutdownAfterClose)
    {
        if (Interlocked.CompareExchange(ref _isHandlingFatalException, 1, 0) != 0)
        {
            return;
        }

        var logPath = TryWriteCrashLog(exception);
        var message =
            $"WindowsNotch crashed.\n\n{exception}\n\nLog:\n{logPath}";

        void ShowMessage()
        {
            MessageBox.Show(
                message,
                "WindowsNotch",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            if (shutdownAfterClose)
            {
                Shutdown(-1);
            }
        }

        try
        {
            if (Dispatcher.CheckAccess())
            {
                ShowMessage();
                return;
            }

            Dispatcher.Invoke(ShowMessage);
        }
        catch
        {
            MessageBox.Show(
                message,
                "WindowsNotch",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (!shutdownAfterClose)
            {
                Interlocked.Exchange(ref _isHandlingFatalException, 0);
            }
        }
    }

    private static string TryWriteCrashLog(Exception exception)
    {
        try
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
        catch (Exception logException)
        {
            return $"Crash log could not be written: {logException.Message}";
        }
    }
}
