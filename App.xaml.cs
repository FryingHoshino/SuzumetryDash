using System.Windows;
using Suzumetry.Core;

namespace Suzumetry;

public partial class App : Application
{
    Mutex? mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        mutex = new Mutex(true, "Suzumetry.SingleInstance", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }
        base.OnStartup(e);
        DispatcherUnhandledException += (_, a) =>
        {
            Log.Write(a.Exception);
            a.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            if (a.ExceptionObject is Exception ex) Log.Write(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Log.Write(a.Exception);
            a.SetObserved();
        };
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { mutex?.ReleaseMutex(); } catch { }
        mutex?.Dispose();
        base.OnExit(e);
    }
}
