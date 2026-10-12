using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace Tekstvask;

/// <summary>The Win32 calls the app needs, plus one hidden window that receives hotkeys and owns the clipboard.</summary>
internal static class Win32
{
    /// <summary>Message-only window: target for WM_HOTKEY, and the clipboard owner (a null owner makes SetClipboardData fail).</summary>
    public static readonly HwndSource Window = new(new HwndSourceParameters("Tekstvask") { WindowStyle = 0, ParentWindow = new IntPtr(-3) });

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);

    [DllImport("user32.dll")]
    public static extern bool MessageBeep(uint type);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>A warning box in front of every window, like NSAlert from a menu bar app.</summary>
    public static void Alert(string text) => MessageBox(IntPtr.Zero, text, "Tekstvask", 0x30 /* ICONWARNING */ | 0x40000 /* TOPMOST */ | 0x10000 /* SETFOREGROUND */);

    // Keyboard

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint Type; public InputUnion U; }

    // The union must be as large as MOUSEINPUT, or SendInput rejects the size.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT Mouse; [FieldOffset(0)] public KEYBDINPUT Keyboard; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int Dx, Dy; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public IntPtr ExtraInfo; }

    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private static readonly int[] ModifierKeys = [VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN];

    private static INPUT Key(int vk, bool down) =>
        new() { Type = 1, U = new() { Keyboard = new() { Vk = (ushort)vk, Flags = down ? 0u : 2u /* KEYUP */ } } };

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>Sends Ctrl+key, e.g. 'C' or 'V'. Virtual keys follow the keyboard layout, so this works on Dvorak too.</summary>
    public static void PressCtrl(char key) =>
        SendInput(4, [Key(VK_CONTROL, true), Key(key, true), Key(key, false), Key(VK_CONTROL, false)], Marshal.SizeOf<INPUT>());

    /// <summary>
    /// Waits until the user lets go of the shortcut's modifier keys, so they don't mix into our Ctrl+C.
    /// A hotkey fires on key down; this is the equivalent of the macOS app acting on key up.
    /// </summary>
    public static async Task ReleaseModifiers()
    {
        // A no-op key while Alt or Win is held stops their release from opening the app's menu bar or the Start menu.
        if (IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
            SendInput(2, [Key(0xE8, true), Key(0xE8, false)], Marshal.SizeOf<INPUT>());
        for (int i = 0; i < 100 && ModifierKeys.Any(IsDown); i++) await Task.Delay(20);
        // Held for over two seconds: release them for the app, or Ctrl+C becomes Ctrl+Shift+Alt+C.
        var held = ModifierKeys.Where(IsDown).Select(vk => Key(vk, false)).ToArray();
        if (held.Length > 0) SendInput((uint)held.Length, held, Marshal.SizeOf<INPUT>());
    }

    // Windows

    public static uint ProcessOf(IntPtr hWnd) => GetWindowThreadProcessId(hWnd, out uint pid) == 0 ? 0 : pid;

    public static string ClassName(IntPtr hWnd)
    {
        var name = new StringBuilder(256);
        return GetClassName(hWnd, name, name.Capacity) > 0 ? name.ToString() : "";
    }

    private delegate void WinEventProc(IntPtr hook, uint e, IntPtr hWnd, int obj, int child, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);

    private static WinEventProc? foregroundHook; // kept alive: the hook calls it from native code

    /// <summary>Calls back on the UI thread whenever another window comes to the front.</summary>
    public static void OnForegroundChange(Action<IntPtr> handler)
    {
        foregroundHook = (_, _, hWnd, _, _, _, _) => handler(hWnd);
        SetWinEventHook(3 /* EVENT_SYSTEM_FOREGROUND */, 3, IntPtr.Zero, foregroundHook, 0, 0, 0 /* OUTOFCONTEXT */);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int width, int height, uint flags);

    /// <summary>Moves a window in physical pixels (what GetCursorPos gives a per-monitor-aware app), keeping it on top and inactive.</summary>
    public static void MoveTopmost(IntPtr hWnd, int x, int y) =>
        SetWindowPos(hWnd, new IntPtr(-1) /* HWND_TOPMOST */, x, y, 0, 0, 0x1 /* NOSIZE */ | 0x10 /* NOACTIVATE */);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

    /// <summary>Never activated, ignored by the mouse, and left out of Alt+Tab.</summary>
    public static void MakeClickThrough(IntPtr hWnd)
    {
        const int GWL_EXSTYLE = -20;
        long style = GetWindowLongPtr(hWnd, GWL_EXSTYLE) | 0x08000000 /* NOACTIVATE */ | 0x20 /* TRANSPARENT */ | 0x80 /* TOOLWINDOW */;
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(style));
    }

    // Clipboard

    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr mem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr mem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr mem);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr mem);

    private const uint CF_UNICODETEXT = 13;

    /// <summary>Opens the clipboard, retrying while another app holds it.</summary>
    private static bool Open()
    {
        for (int i = 0; i < 25; i++)
        {
            if (OpenClipboard(Window.Handle)) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    /// <summary>
    /// Formats whose data isn't a plain memory block: GDI handles (bitmap, palette, metafiles), owner-display formats
    /// and private ranges. They can't be copied byte for byte; CF_DIB and the others carry the same content.
    /// </summary>
    private static bool IsCopyable(uint format) =>
        format is not (2 or 3 or 9 or 14 or 0x80 or 0x82 or 0x83 or 0x8E) and not (>= 0x200 and <= 0x3FF);

    public static List<(uint Format, byte[] Data)> ReadAll()
    {
        var items = new List<(uint, byte[])>();
        if (!Open()) return items;
        try
        {
            for (uint format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
            {
                if (!IsCopyable(format)) continue;
                IntPtr handle = GetClipboardData(format);
                IntPtr source = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
                if (source == IntPtr.Zero) continue;
                try
                {
                    var data = new byte[(int)GlobalSize(handle)];
                    Marshal.Copy(source, data, 0, data.Length);
                    items.Add((format, data));
                }
                finally { GlobalUnlock(handle); }
            }
        }
        finally { CloseClipboard(); }
        return items;
    }

    public static string? ReadText()
    {
        if (!Open()) return null;
        try
        {
            IntPtr handle = GetClipboardData(CF_UNICODETEXT);
            IntPtr text = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
            if (text == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(text); }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Replaces the clipboard. With <paramref name="transient"/> it stays out of Win+V history and cloud sync.</summary>
    public static void WriteAll(IEnumerable<(uint Format, byte[] Data)> items, bool transient)
    {
        if (!Open()) return;
        try
        {
            EmptyClipboard();
            foreach (var (format, data) in items) Set(format, data);
            if (transient)
            {
                Set(RegisterClipboardFormat("CanIncludeInClipboardHistory"), new byte[4]);
                Set(RegisterClipboardFormat("CanUploadToCloudClipboard"), new byte[4]);
            }
        }
        finally { CloseClipboard(); }
    }

    public static void WriteText(string text, bool transient) =>
        WriteAll([(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + "\0"))], transient);

    private static void Set(uint format, byte[] data)
    {
        IntPtr handle = GlobalAlloc(0x0002 /* MOVEABLE */, (UIntPtr)Math.Max(data.Length, 1));
        IntPtr target = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
        if (target == IntPtr.Zero) return;
        Marshal.Copy(data, 0, target, data.Length);
        GlobalUnlock(handle);
        if (SetClipboardData(format, handle) == IntPtr.Zero) GlobalFree(handle); // on success the clipboard owns it
    }
}
