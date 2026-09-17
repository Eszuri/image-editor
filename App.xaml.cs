using System.IO;
using System.Windows;

namespace ImageEditor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string AppID);

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        try
        {
            SetCurrentProcessExplicitAppUserModelID("ImageEditor.DesktopEditor");
        }
        catch { }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            File.WriteAllText("crash.log", args.ExceptionObject.ToString());
        };

        DispatcherUnhandledException += (s, args) =>
        {
            File.WriteAllText("crash.log", args.Exception.ToString());
            args.Handled = false;
        };

        base.OnStartup(e);
    }
}

