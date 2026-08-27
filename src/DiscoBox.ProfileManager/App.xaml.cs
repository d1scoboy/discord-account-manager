using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DiscoBox.ProfileManager;

public partial class App : Application
{
    public static string? StartupPage { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0)
        {
            StartupPage = e.Args[0];
        }

        try
        {
            Services.AppServices.Init();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Failed to initialize storage: {exception.Message}", "DiscoBox",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Services.AppServices.OpenAccount.CloseAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash(e.Exception);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            WriteCrash(exception);
        }
    }

    private static void WriteCrash(Exception exception)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiscoBox", "logs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "crash.log");
            File.AppendAllText(file,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch
        {
        }
    }
}
