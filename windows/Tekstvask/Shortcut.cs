using System.Numerics;

namespace Tekstvask;

/// <summary>Values are RegisterHotKey's MOD_* flags.</summary>
[Flags]
public enum Modifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }

/// <summary>A global shortcut: modifiers plus a Win32 virtual-key code. Stored in settings as its display text.</summary>
public sealed record Shortcut(Modifiers Modifiers, int Key)
{
    // Ctrl+Alt+letter is AltGr on Nordic keyboards, so the defaults add Shift.
    public static readonly Shortcut DefaultProofread = new(Modifiers.Control | Modifiers.Alt | Modifiers.Shift, 'L');
    public static readonly Shortcut DefaultTranslate = new(Modifiers.Control | Modifiers.Alt | Modifiers.Shift, 'T');

    // Windows' own order: Win+Ctrl+Alt+Shift.
    private static readonly (Modifiers Flag, string Name)[] ModifierNames =
        [(Modifiers.Win, "Win"), (Modifiers.Control, "Ctrl"), (Modifiers.Alt, "Alt"), (Modifiers.Shift, "Shift")];

    private static readonly Dictionary<int, string> NamedKeys = new()
    {
        [0x20] = "Space", [0x21] = "PgUp", [0x22] = "PgDn", [0x23] = "End", [0x24] = "Home",
        [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down", [0x2D] = "Insert", [0x2E] = "Delete",
    };

    private static readonly Dictionary<string, int> KeysByName =
        Enumerable.Range(0, 256).Where(k => KeyName(k) != null).ToDictionary(k => KeyName(k)!, StringComparer.OrdinalIgnoreCase);

    /// <summary>Letters, digits, F1–F24 and a few navigation keys. Punctuation keys differ per keyboard layout, so they're not offered.</summary>
    public static string? KeyName(int key) => key switch
    {
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        _ => NamedKeys.GetValueOrDefault(key),
    };

    public override string ToString() =>
        string.Join("+", ModifierNames.Where(m => Modifiers.HasFlag(m.Flag)).Select(m => m.Name).Append(KeyName(Key) ?? $"#{Key}"));

    public static Shortcut? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var modifiers = Modifiers.None;
        string[] parts = text.Split('+');
        foreach (string part in parts[..^1])
        {
            var match = ModifierNames.FirstOrDefault(m => m.Name.Equals(part.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match.Name == null) return null;
            modifiers |= match.Flag;
        }
        return KeysByName.TryGetValue(parts[^1].Trim(), out int key) ? new Shortcut(modifiers, key) : null;
    }

    /// <summary>Why this can't be a global shortcut (English key for <see cref="Strings.T(string)"/>), or null if it can.</summary>
    public string? Problem()
    {
        // One modifier would steal ordinary app shortcuts such as Ctrl+C.
        if (KeyName(Key) == null || BitOperations.PopCount((uint)Modifiers) < 2)
            return "Use at least two of Ctrl, Alt, Shift and Win together with a letter, digit or function key.";
        if (Modifiers == (Modifiers.Control | Modifiers.Alt))
            return "Ctrl+Alt is AltGr on many keyboards. Add Shift or Win.";
        return null;
    }
}
