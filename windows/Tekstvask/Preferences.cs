using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Tekstvask;

/// <summary>Settings in %APPDATA%\Tekstvask\settings.json; the API key next to it, encrypted for the Windows user with DPAPI.</summary>
internal static class Preferences
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tekstvask");
    private static readonly string SettingsFile = Path.Combine(Folder, "settings.json");
    private static readonly string KeyFile = Path.Combine(Folder, "api-key.bin");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed class Data
    {
        public string? TargetLanguage { get; set; }
        public string? SecondLanguage { get; set; }
        public string? Model { get; set; }
        public string? CustomName { get; set; }
        /// <summary>Action name → shortcut text. A missing entry means the default; an empty one means none.</summary>
        public Dictionary<string, string> Shortcuts { get; set; } = [];
        /// <summary>Action name → the user's own prompt, stored only while it differs from the default.</summary>
        public Dictionary<string, string> Prompts { get; set; } = [];
    }

    private static readonly Data data = Load();

    private static Data Load()
    {
        try { return JsonSerializer.Deserialize<Data>(File.ReadAllText(SettingsFile), Json) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException) { return new(); }
    }

    private static void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(data, Json));
    }

    public static string TargetLanguage
    {
        get => data.TargetLanguage ?? (Strings.SystemLanguage == "en" ? "nb" : "en");
        set { data.TargetLanguage = value; Save(); }
    }

    /// <summary>What text that's already in the target language is translated to.</summary>
    public static string SecondLanguage
    {
        get => Strings.SecondLanguage(data.SecondLanguage, TargetLanguage);
        set { data.SecondLanguage = value; Save(); }
    }

    /// <summary>One of the models offered; anything else (older versions let you set any) means the default.</summary>
    public static string Model
    {
        get => Claude.Models.Contains(data.Model) ? data.Model! : Claude.DefaultModel;
        set { data.Model = value; Save(); }
    }

    /// <summary>The custom action's name in the menu; empty while it's unused.</summary>
    public static string CustomName
    {
        get => data.CustomName?.Trim() ?? "";
        set { data.CustomName = value; Save(); }
    }

    public static string GetPrompt(TextAction action) =>
        data.Prompts.TryGetValue(action.ToString(), out string? prompt) && !string.IsNullOrWhiteSpace(prompt) ? prompt : action.DefaultPrompt();

    public static void SetPrompt(TextAction action, string prompt)
    {
        if (prompt == action.DefaultPrompt() || string.IsNullOrWhiteSpace(prompt)) data.Prompts.Remove(action.ToString());
        else data.Prompts[action.ToString()] = prompt;
        Save();
    }

    public static Shortcut? GetShortcut(TextAction action) =>
        data.Shortcuts.TryGetValue(action.ToString(), out string? text) ? Shortcut.Parse(text) : action.DefaultShortcut();

    public static void SetShortcut(TextAction action, Shortcut? shortcut)
    {
        data.Shortcuts[action.ToString()] = shortcut?.ToString() ?? "";
        Save();
    }

    private static readonly byte[] Entropy = "Tekstvask"u8.ToArray();

    public static string? ApiKey
    {
        get
        {
            try
            {
                string key = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), Entropy, DataProtectionScope.CurrentUser));
                return key.Length > 0 ? key : null;
            }
            catch (Exception e) when (e is IOException or CryptographicException) { return null; }
        }
        set
        {
            if (string.IsNullOrEmpty(value)) { File.Delete(KeyFile); return; }
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(KeyFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
        }
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool OpenAtLogin
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("Tekstvask") != null;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue("Tekstvask", $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue("Tekstvask", throwOnMissingValue: false);
        }
    }
}
