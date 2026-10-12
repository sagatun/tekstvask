using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Forms = System.Windows.Forms;

namespace Tekstvask;

/// <summary>
/// A small "Working…" label by the mouse pointer while Claude works. Windows 11 tucks new tray icons into the overflow,
/// so the busy tray icon alone is easy to miss. The label never takes focus and lets clicks through, so the user's app
/// stays in front for the paste.
/// </summary>
internal sealed class WorkingPill
{
    private readonly Window window;

    private WorkingPill()
    {
        var text = new TextBlock { Text = Strings.T("Working…"), Foreground = Brushes.White };
        // A slow pulse, so it reads as "in progress" rather than as a stuck label.
        text.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, 0.5, TimeSpan.FromSeconds(0.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Left = -32000, // off screen until it's placed by the pointer
            Top = -32000,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xB8, 0x5C, 0x44)), // the app icon's terracotta
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(11, 4, 11, 5),
                Margin = new Thickness(8), // room for the shadow
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 },
                Child = text,
            },
        };
        window.SourceInitialized += (_, _) => Win32.MakeClickThrough(new WindowInteropHelper(window).Handle);
    }

    public static WorkingPill ShowNearCursor()
    {
        var pill = new WorkingPill();
        pill.window.Show();
        pill.MoveNearCursor();
        return pill;
    }

    /// <summary>Safe to call more than once.</summary>
    public void Close()
    {
        if (window.IsVisible) window.Close();
    }

    private void MoveNearCursor()
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        Win32.GetCursorPos(out var cursor);
        Win32.GetWindowRect(handle, out var rect);
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y)).WorkingArea;
        // Below and to the right of the pointer, flipped where that would leave the screen.
        int x = cursor.X + 12, y = cursor.Y + 14;
        if (x + width > screen.Right) x = cursor.X - width;
        if (y + height > screen.Bottom) y = cursor.Y - height;
        Win32.MoveTopmost(handle, x, y);
    }
}
