namespace Tekstvask;

public enum TextAction { Proofread, Translate, Custom }

internal static class TextActionInfo
{
    public static Shortcut? DefaultShortcut(this TextAction action) => action switch
    {
        TextAction.Proofread => Shortcut.DefaultProofread,
        TextAction.Translate => Shortcut.DefaultTranslate,
        _ => null,
    };

    public static string Title(this TextAction action) => action switch
    {
        TextAction.Proofread => Strings.T("Proofread"),
        TextAction.Translate => Strings.T("Translate to {0}", Strings.LanguageName(Preferences.TargetLanguage)),
        _ => Preferences.CustomName,
    };

    /// <summary>The custom action is in use once it has a name.</summary>
    public static bool IsAvailable(this TextAction action) => action != TextAction.Custom || Preferences.CustomName.Length > 0;

    public static IEnumerable<TextAction> Available => Enum.GetValues<TextAction>().Where(IsAvailable);

    public static string DefaultPrompt(this TextAction action) => action switch
    {
        TextAction.Proofread => Prompts.Proofread,
        TextAction.Translate => Prompts.Translate,
        _ => Prompts.Custom,
    };

    /// <summary>Translate is two-way, so its languages depend on the text; the other prompts get the settings as they are.</summary>
    public static string SystemPrompt(this TextAction action, string text)
    {
        var (target, second) = (Preferences.TargetLanguage, Preferences.SecondLanguage);
        if (action == TextAction.Translate) (target, second) = Prompts.Languages(text, target, second);
        return Prompts.Filling(Preferences.GetPrompt(action), target, second);
    }
}

/// <summary>
/// Copies the selection in the foreground app, rewrites it with Claude and pastes the result over it.
/// Undo is the target app's own Ctrl+Z.
/// </summary>
internal sealed class TextActions
{
    public bool IsBusy { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;
    /// <summary>Something worth a passing notification rather than an alert.</summary>
    public event Action<string>? Notice;

    private IntPtr lastOtherWindow = Win32.GetForegroundWindow();

    public TextActions()
    {
        // Clicking the tray icon makes the taskbar the foreground window, so remember where the user was before.
        Win32.OnForegroundChange(window =>
        {
            string type = Win32.ClassName(window);
            bool isShell = type.StartsWith("Shell_") || type is "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland";
            if (!isShell && Win32.ProcessOf(window) != Environment.ProcessId) lastOtherWindow = window;
        });
    }

    public async void Start(TextAction action)
    {
        if (IsBusy) { Win32.MessageBeep(0); return; }
        await Win32.ReleaseModifiers();
        await Run(action);
    }

    /// <summary>From the tray menu: give focus back to the window the user came from, let it settle, then run.</summary>
    public async void StartFromMenu(TextAction action)
    {
        if (Win32.IsWindow(lastOtherWindow)) Win32.SetForegroundWindow(lastOtherWindow);
        await Task.Delay(200);
        await Run(action);
    }

    public void ReportError(string? message)
    {
        LastError = message;
        Changed?.Invoke();
    }

    private async Task Run(TextAction action)
    {
        if (!action.IsAvailable()) return;
        if (IsBusy) { Win32.MessageBeep(0); return; }
        if (Preferences.ApiKey is not { } apiKey) { SettingsWindow.Open(); return; }
        IsBusy = true;
        Changed?.Invoke();
        try
        {
            var clipboard = new BorrowedClipboard();
            IntPtr window = Win32.GetForegroundWindow();
            if (await clipboard.CopySelection() is not { } original) { Win32.MessageBeep(0); return; }
            string? error = null;
            var pill = WorkingPill.ShowNearCursor();
            try
            {
                string reply = await Claude.Complete(apiKey, Preferences.Model, action.SystemPrompt(original), original);
                pill.Close();
                string result = Prompts.MatchingLineEndings(original, Prompts.RestoringOuterWhitespace(original, reply));
                if (Win32.GetForegroundWindow() != window)
                {
                    clipboard.Keep(result);
                    // Not an alert: the result is safe, and an alert would pull the user away from where they went.
                    string message = Strings.T("You switched apps, so the result was copied to the clipboard instead.");
                    ReportError(message);
                    Notice?.Invoke(message);
                    return;
                }
                await clipboard.Paste(result);
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            finally
            {
                pill.Close();
            }
            // Give the user's clipboard back before an alert can hold things up.
            clipboard.Restore();
            if (error != null) Report(error);
            else ReportError(null);
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    private void Report(string message)
    {
        ReportError(message);
        Win32.Alert(message);
    }
}

/// <summary>Borrows the clipboard to copy and paste, then puts the user's own clipboard back.</summary>
internal sealed class BorrowedClipboard
{
    // ponytail: reads every format eagerly, so a huge copied file or image is held in memory for a moment.
    private readonly List<(uint Format, byte[] Data)> saved = Win32.ReadAll();
    private uint sequence = Win32.GetClipboardSequenceNumber(); // the last change we caused; anything newer is the user's

    /// <summary>Sends Ctrl+C and waits up to a second for the app to copy. Null when nothing (or only whitespace) is selected.</summary>
    public async Task<string?> CopySelection()
    {
        uint before = Win32.GetClipboardSequenceNumber();
        Win32.PressCtrl('C');
        for (int i = 0; i < 50 && Win32.GetClipboardSequenceNumber() == before; i++) await Task.Delay(20);
        if (Win32.GetClipboardSequenceNumber() == before) return null;
        await Task.Delay(30); // some apps write several formats one after another
        string? text = Win32.ReadText();
        sequence = Win32.GetClipboardSequenceNumber();
        if (string.IsNullOrWhiteSpace(text))
        {
            Restore();
            return null;
        }
        return text;
    }

    public async Task Paste(string text)
    {
        Win32.WriteText(text, transient: true);
        sequence = Win32.GetClipboardSequenceNumber();
        Win32.PressCtrl('V');
        await Task.Delay(500); // let the app read the clipboard before we restore it
    }

    public void Keep(string text)
    {
        Win32.WriteText(text, transient: false);
        sequence = Win32.GetClipboardSequenceNumber();
    }

    public void Restore()
    {
        if (Win32.GetClipboardSequenceNumber() != sequence) return; // the user copied something new meanwhile; keep it
        // Transient: it is already in Win+V history from when the user copied it.
        Win32.WriteAll(saved, transient: true);
    }
}
