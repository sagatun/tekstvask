using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Tekstvask;

/// <summary>
/// The notification-area icon. WinForms provides the icon itself; the menu is WPF so it gets the Windows 11 look.
/// </summary>
internal sealed class Tray : IDisposable
{
    private readonly TextActions actions;
    private readonly Forms.NotifyIcon icon;
    private readonly Drawing.Icon normal;
    private readonly Drawing.Icon busy;

    public Tray(TextActions actions)
    {
        this.actions = actions;
        var size = Forms.SystemInformation.SmallIconSize;
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Tekstvask.ico")).Stream)
            normal = new Drawing.Icon(stream, size);
        busy = BusyIcon(size.Width);
        icon = new Forms.NotifyIcon { Icon = normal, Text = "Tekstvask", Visible = true };
        icon.MouseUp += (_, _) => ShowMenu();
        actions.Notice += ShowHint;
        actions.Changed += () =>
        {
            icon.Icon = actions.IsBusy ? busy : normal;
            icon.Text = actions.IsBusy ? $"Tekstvask – {Strings.T("Working…")}" : "Tekstvask";
        };
    }

    /// <summary>A notification from the tray icon, e.g. to tell first-time users where the app went (Windows 11 tucks new icons into the overflow).</summary>
    public void ShowHint(string text) => icon.ShowBalloonTip(10_000, "Tekstvask", text, Forms.ToolTipIcon.None);

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        foreach (var action in TextActionInfo.Available)
            Add(menu.Items, action.Title(), () => actions.StartFromMenu(action)).InputGestureText = Preferences.GetShortcut(action)?.ToString() ?? "";

        var languages = new MenuItem { Header = Strings.T("Target language") };
        foreach (string code in Strings.LanguageCodes)
        {
            string name = Strings.LanguageName(code);
            var item = Add(languages.Items, name[..1].ToUpperInvariant() + name[1..], () => Preferences.TargetLanguage = code);
            item.IsCheckable = true; // the Fluent theme draws the check mark only on checkable items
            item.IsChecked = code == Preferences.TargetLanguage;
        }
        menu.Items.Add(languages);
        menu.Items.Add(new Separator());

        if (actions.LastError is { } error)
        {
            menu.Items.Add(new MenuItem { Header = new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 }, IsEnabled = false });
            menu.Items.Add(new Separator());
        }
        Add(menu.Items, Strings.T("Settings…"), SettingsWindow.Open);
        Add(menu.Items, Strings.T("Quit Tekstvask"), () => Application.Current.Shutdown());

        menu.IsOpen = true;
        // A click elsewhere closes the menu only while the app is in front, so bring it there.
        if (PresentationSource.FromVisual(menu) is HwndSource source) Win32.SetForegroundWindow(source.Handle);
    }

    private static MenuItem Add(ItemCollection items, string title, Action onClick)
    {
        // A TextBlock header so an underscore in the text isn't taken as an access key.
        var item = new MenuItem { Header = new TextBlock { Text = title } };
        item.Click += (_, _) => onClick();
        items.Add(item);
        return item;
    }

    /// <summary>The app icon's colours with an ellipsis, for while Claude works (the macOS app shows ellipsis.circle).</summary>
    private static Drawing.Icon BusyIcon(int size)
    {
        using var bitmap = new Drawing.Bitmap(size, size);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = size * 0.45f; // corner diameter, about the app icon's
            using var shape = new GraphicsPath();
            shape.AddArc(0, 0, r, r, 180, 90);
            shape.AddArc(size - 1 - r, 0, r, r, 270, 90);
            shape.AddArc(size - 1 - r, size - 1 - r, r, r, 0, 90);
            shape.AddArc(0, size - 1 - r, r, r, 90, 90);
            shape.CloseFigure();
            using var fill = new LinearGradientBrush(new Drawing.Point(0, 0), new Drawing.Point(0, size),
                Drawing.Color.FromArgb(0xD4, 0x74, 0x54), Drawing.Color.FromArgb(0xA7, 0x4C, 0x38));
            g.FillPath(fill, shape);
            float dot = Math.Max(2f, size * 0.16f);
            foreach (float x in new[] { 0.27f, 0.5f, 0.73f })
                g.FillEllipse(Drawing.Brushes.White, size * x - dot / 2, (size - dot) / 2, dot, dot);
        }
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }
}
