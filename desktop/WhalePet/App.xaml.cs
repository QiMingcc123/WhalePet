using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace WhalePet;

public partial class App : Application
{
    private const string MutexName = @"Local\DSHWhalePet.SingleInstance";
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        AssetBootstrapper.Ensure();
        PersistentStore.CleanupBackups();
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var splash = new SplashWindow();
            await splash.ShowAsync();

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception error)
        {
            MessageBox.Show("启动失败：" + error.Message, "鲸鱼娘", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
            // The mutex may already have been released during shutdown.
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void ActivateExistingInstance()
    {
        var handle = FindWindow(null, "鲸鱼娘");
        if (handle == IntPtr.Zero) return;
        ShowWindow(handle, 9);
        SetForegroundWindow(handle);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}
