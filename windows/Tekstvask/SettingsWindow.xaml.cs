using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Tekstvask;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? current;

    /// <summary>Shows the one Settings window, from anywhere (first launch, missing API key, tray menu), in front.</summary>
    public static void Open()
    {
        current ??= new SettingsWindow();
        current.Show();
        if (current.WindowState == WindowState.Minimized) current.WindowState = WindowState.Normal;
        current.Activate();
    }

    private readonly Dictionary<TextAction, (TextBlock Label, Button Recorder, Button Clear)> rows = [];
    private TextAction? recording;

    private SettingsWindow()
    {
        InitializeComponent();
        Closed += (_, _) => { StopRecording(); current = null; };
        // The window follows each tab's height; keep it on screen, and let a tab scroll on a small screen.
        MaxHeight = SystemParameters.WorkArea.Height;
        SizeChanged += (_, _) =>
        {
            var screen = SystemParameters.WorkArea;
            if (Top + ActualHeight > screen.Bottom) Top = Math.Max(screen.Top, screen.Bottom - ActualHeight);
        };
        // The recorder swallows the key pressed with Alt, so Windows takes the Alt release for a lone Alt tap and enters
        // menu mode, which eats the next click. This window has no menu, so ignore that.
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(
            (IntPtr _, int message, IntPtr command, IntPtr key, ref bool handled) =>
            {
                handled |= message == 0x0112 /* WM_SYSCOMMAND */ && ((int)command & 0xFFF0) == 0xF100 /* SC_KEYMENU */ && key == IntPtr.Zero;
                return IntPtr.Zero;
            });

        ApiKey.Password = Preferences.ApiKey ?? "";
        OpenAtLogin.IsChecked = Preferences.OpenAtLogin;
        var languages = Strings.LanguageCodes.ToDictionary(code => code, code =>
        {
            string name = Strings.LanguageName(code);
            return name[..1].ToUpperInvariant() + name[1..];
        });
        LanguagePicker.ItemsSource = languages;
        LanguagePicker.SelectedValue = Preferences.TargetLanguage;
        SecondLanguagePicker.ItemsSource = languages;

        foreach (var action in Enum.GetValues<TextAction>()) AddRow(action);
        for (int i = 0; i < 3; i++) Shortcuts.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(LanguageLabel, rows.Count);
        Grid.SetRow(LanguagePicker, rows.Count);
        Grid.SetRow(SecondLanguageLabel, rows.Count + 1);
        Grid.SetRow(SecondLanguagePicker, rows.Count + 1);
        Grid.SetRow(LanguageCaption, rows.Count + 2);

        // Advanced
        ModelPicker.ItemsSource = new Dictionary<string, string>
        {
            ["claude-haiku-5-5"] = "Claude Haiku 5.5",
            ["claude-sonnet-5-5"] = "Claude Sonnet 5.5",
            ["claude-opus-5-5"] = "Claude Opus 5.5",
        };
        ModelPicker.SelectedValue = Preferences.Model;
        CustomName.Text = Preferences.CustomName;
        PlaceholderCaption.Text = Strings.T("{0} becomes the language to translate to and {1} the other language.",
            Prompts.TargetPlaceholder, Prompts.SecondPlaceholder);
        // Segmented like the macOS picker: radio buttons drawn as toggle buttons.
        var segment = TryFindResource(typeof(ToggleButton)) as Style;
        foreach (var action in Enum.GetValues<TextAction>())
        {
            var kind = new RadioButton
            {
                Content = action switch
                {
                    TextAction.Proofread => Strings.T("Proofread"),
                    TextAction.Translate => Strings.T("Translate"),
                    _ => Strings.T("Custom"),
                },
                GroupName = "Prompt",
                Style = segment,
                Margin = new Thickness(0, 0, 6, 0),
                MinWidth = 90,
                IsChecked = action == editing,
            };
            kind.Checked += (_, _) => EditPrompt(action);
            PromptKinds.Children.Add(kind);
        }
        EditPrompt(editing);
        RefreshLanguages();
    }

    private TextAction editing = TextAction.Proofread;

    private void EditPrompt(TextAction action)
    {
        editing = action;
        PromptEditor.Text = Preferences.GetPrompt(action); // also saves it again, unchanged
    }

    private void PromptChanged(object sender, TextChangedEventArgs e)
    {
        Preferences.SetPrompt(editing, PromptEditor.Text);
        ResetPrompt.IsEnabled = PromptEditor.Text != editing.DefaultPrompt();
    }

    private void ResetPromptClicked(object sender, RoutedEventArgs e) => PromptEditor.Text = editing.DefaultPrompt();

    private void ModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelPicker.SelectedValue is string model && model != Preferences.Model) Preferences.Model = model;
    }

    private void SecondLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecondLanguagePicker.SelectedValue is not string code || code == Preferences.SecondLanguage) return;
        Preferences.SecondLanguage = code;
        RefreshLanguages();
    }

    /// <summary>The second language depends on the target, and the caption on both.</summary>
    private void RefreshLanguages()
    {
        SecondLanguagePicker.SelectedValue = Preferences.SecondLanguage;
        LanguageCaption.Text = Strings.T("Text in {0} is translated to {1} instead.",
            Strings.LanguageName(Preferences.TargetLanguage), Strings.LanguageName(Preferences.SecondLanguage));
    }

    private void CustomNameChanged(object sender, TextChangedEventArgs e)
    {
        CustomNamePlaceholder.Visibility = CustomName.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        bool wasAvailable = TextAction.Custom.IsAvailable();
        Preferences.CustomName = CustomName.Text;
        if (rows.Count > 0) Refresh(TextAction.Custom);
        if (wasAvailable != TextAction.Custom.IsAvailable() && !Hotkeys.Register(TextAction.Custom))
            ShowProblem(Strings.T("That shortcut is taken by Windows or another app."));
    }

    private void AddRow(TextAction action)
    {
        int row = Shortcuts.RowDefinitions.Count;
        Shortcuts.RowDefinitions.Add(new RowDefinition());
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 8) };
        var recorder = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
        var clear = new Button
        {
            Content = "", // Cancel glyph
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            ToolTip = Strings.T("Remove shortcut"),
            Margin = new Thickness(6, 0, 0, 8),
        };
        recorder.Click += (_, _) => StartRecording(action);
        recorder.PreviewKeyDown += (_, e) => Record(action, e);
        recorder.LostKeyboardFocus += (_, _) => StopRecording();
        clear.Click += (_, _) => SetShortcut(action, null);
        foreach (var (element, column) in new (UIElement, int)[] { (label, 0), (recorder, 1), (clear, 2) })
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Shortcuts.Children.Add(element);
        }
        rows[action] = (label, recorder, clear);
        Refresh(action);
    }

    private void Refresh(TextAction action)
    {
        var (label, recorder, clear) = rows[action];
        label.Text = action.Title();
        recorder.Content = recording == action ? Strings.T("Type a shortcut…") : Preferences.GetShortcut(action)?.ToString() ?? "";
        var visibility = action.IsAvailable() ? Visibility.Visible : Visibility.Collapsed;
        label.Visibility = recorder.Visibility = clear.Visibility = visibility;
    }

    private void StartRecording(TextAction action)
    {
        StopRecording();
        // Off while recording, so pressing a current shortcut reaches the recorder instead of running the action.
        Hotkeys.UnregisterAll();
        recording = action;
        Refresh(action);
        rows[action].Recorder.Focus();
    }

    private void StopRecording()
    {
        if (recording is not { } action) return;
        recording = null;
        Refresh(action);
        Hotkeys.RegisterAll();
    }

    private void Record(TextAction action, KeyEventArgs e)
    {
        if (recording != action) return;
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin) return; // wait for the key that completes the shortcut
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None) { StopRecording(); return; }

        var modifiers = Modifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= Modifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= Modifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= Modifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= Modifiers.Win;
        SetShortcut(action, new Shortcut(modifiers, KeyInterop.VirtualKeyFromKey(key)));
    }

    private void SetShortcut(TextAction action, Shortcut? shortcut)
    {
        string? problem = shortcut?.Problem() is { } rule ? Strings.T(rule) : null;
        var other = TextActionInfo.Available.Where(a => a != action && shortcut != null && Preferences.GetShortcut(a) == shortcut);
        if (problem == null && other.Any()) problem = Strings.T("That shortcut is already used by “{0}”.", other.First().Title());
        if (problem == null)
        {
            var previous = Preferences.GetShortcut(action);
            Preferences.SetShortcut(action, shortcut);
            if (!Hotkeys.Register(action))
            {
                Preferences.SetShortcut(action, previous);
                problem = Strings.T("That shortcut is taken by Windows or another app.");
            }
            // An unnamed custom action keeps its shortcut for later, but gives it up rather than share it.
            else if (shortcut != null && !TextAction.Custom.IsAvailable() && Preferences.GetShortcut(TextAction.Custom) == shortcut)
                Preferences.SetShortcut(TextAction.Custom, null);
        }
        ShowProblem(problem);
        StopRecording();
        Refresh(action);
        Keyboard.ClearFocus();
    }

    private void ShowProblem(string? problem)
    {
        ShortcutProblem.Text = problem;
        ShortcutProblem.Visibility = problem == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApiKeyChanged(object sender, RoutedEventArgs e) => Preferences.ApiKey = ApiKey.Password.Trim();

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguagePicker.SelectedValue is not string code || code == Preferences.TargetLanguage) return;
        Preferences.TargetLanguage = code;
        if (rows.Count > 0) Refresh(TextAction.Translate);
        if (IsInitialized) RefreshLanguages();
    }

    private void OpenAtLoginClicked(object sender, RoutedEventArgs e) => Preferences.OpenAtLogin = OpenAtLogin.IsChecked == true;

    private void OpenLink(object sender, RequestNavigateEventArgs e) =>
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
}

/// <summary>UI text in XAML: <c>Text="{local:Tr 'Open at login'}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.T(text);
}
