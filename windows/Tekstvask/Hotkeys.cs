namespace Tekstvask;

/// <summary>
/// Global shortcuts through RegisterHotKey. Windows refuses a shortcut another app (or Windows) already holds,
/// so conflicts are detected here, which macOS can't do for third-party apps.
/// </summary>
internal static class Hotkeys
{
    public static event Action<TextAction>? Pressed;

    static Hotkeys()
    {
        Win32.Window.AddHook((IntPtr _, int message, IntPtr id, IntPtr _, ref bool handled) =>
        {
            if (message == Win32.WM_HOTKEY && Enum.IsDefined((TextAction)((int)id - 1)))
            {
                Pressed?.Invoke((TextAction)((int)id - 1));
                handled = true;
            }
            return IntPtr.Zero;
        });
    }

    /// <summary>Registers the action's saved shortcut. False if another app holds it.</summary>
    public static bool Register(TextAction action)
    {
        Unregister(action);
        // An unnamed custom action keeps its shortcut for later, but mustn't take the keys from other apps meanwhile.
        return !action.IsAvailable() || Preferences.GetShortcut(action) is not { } shortcut
            || Win32.RegisterHotKey(Win32.Window.Handle, Id(action), (uint)shortcut.Modifiers | Win32.MOD_NOREPEAT, (uint)shortcut.Key);
    }

    public static void Unregister(TextAction action) => Win32.UnregisterHotKey(Win32.Window.Handle, Id(action));

    private static int Id(TextAction action) => (int)action + 1;

    /// <summary>
    /// Registers every action and reports the ones Windows refused. Also used to resume after recording a shortcut,
    /// when the hotkeys are off so pressing the current one reaches the recorder.
    /// </summary>
    public static IEnumerable<Shortcut> RegisterAll() =>
        Enum.GetValues<TextAction>().Where(action => !Register(action)).Select(action => Preferences.GetShortcut(action)!).ToList();

    public static void UnregisterAll()
    {
        foreach (var action in Enum.GetValues<TextAction>()) Unregister(action);
    }
}
