using System.Windows;

namespace Tekstvask;

public partial class App : Application
{
    private Mutex? instance;
    private Tray? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\Tekstvask", out bool first);
        var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Tekstvask.ShowSettings");
        if (!first)
        {
            // Opening the app again shows Settings in the copy that's already running.
            Win32.AllowSetForegroundWindow(-1 /* ASFW_ANY */);
            showSettings.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(showSettings, (_, _) => Dispatcher.Invoke(SettingsWindow.Open), null, -1, false);
        DispatcherUnhandledException += (_, args) =>
        {
            Win32.Alert(args.Exception.Message);
            args.Handled = true;
        };

        var actions = new TextActions();
        tray = new Tray(actions);
        Hotkeys.Pressed += actions.Start;
        var taken = Hotkeys.RegisterAll().ToList();
        if (taken.Count > 0)
            actions.ReportError(Strings.T("{0} is taken by Windows or another app. Choose another shortcut in Settings.", string.Join(", ", taken)));
        if (Preferences.OpenAtLogin) Preferences.OpenAtLogin = true; // points the login item at this copy, in case the .exe moved

        if (Preferences.ApiKey == null)
        {
            SettingsWindow.Open();
            tray.ShowHint(Strings.T("Tekstvask runs in the notification area. Select text in any app and press {0}.",
                Preferences.GetShortcut(TextAction.Proofread)?.ToString() ?? ""));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose(); // otherwise the icon lingers in the notification area until the mouse passes over it
        instance?.Dispose();
        base.OnExit(e);
    }
}
