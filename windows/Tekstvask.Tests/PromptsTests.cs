using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Tekstvask.Tests;

public class PromptsTests
{
    [Fact]
    public void TranslatePromptNamesBothLanguagesInEnglish()
    {
        string prompt = Prompts.Filling(Prompts.Translate, "nb", "sv");
        Assert.Contains("Translate it into Norwegian Bokmål. If it is already in Norwegian Bokmål, translate it into Swedish instead.", prompt);
        Assert.DoesNotContain("{", prompt);
    }

    [WindowsFact]
    public void TextInTheTargetLanguageSwapsTheLanguages()
    {
        const string english = "This is a test to see if the language wash works.";
        Assert.Equal(("nb", "en"), Prompts.Languages(english, "en", "nb"));
        Assert.Equal(("en", "nb"), Prompts.Languages("dettte er en ttest for å sse om spraakvash fungerer", "en", "nb"));
        Assert.Equal(("nb", "en"), Prompts.Languages(english, "nb", "en"));
    }

    // Too short to recognize: the prompt's own "if it is already in" clause decides. On macOS there's no recognizer at all.
    [Fact]
    public void UnrecognizedTextKeepsTheLanguages() => Assert.Equal(("en", "nb"), Prompts.Languages("ok", "en", "nb"));

    [Fact]
    public void SecondLanguageIsNeverTheTarget()
    {
        Assert.Equal("de", Strings.SecondLanguage("de", "en", "nb"));
        // Falls back to the system language, then to English, or Norwegian when the target is English.
        Assert.Equal("nb", Strings.SecondLanguage("en", "en", "nb"));
        Assert.Equal("en", Strings.SecondLanguage(null, "nb", "nb"));
        Assert.Equal("nb", Strings.SecondLanguage(null, "en", "en"));
        Assert.Equal("nb", Strings.SecondLanguage(null, "en", "ja"));
        Assert.Equal("de", Strings.SecondLanguage(null, "en", "de"));
    }

    [Fact]
    public void KeepsSelectionsOuterWhitespace()
    {
        Assert.Equal("  Hei, verden!\n", Prompts.RestoringOuterWhitespace("  hei verden\n", "\nHei, verden!  "));
        Assert.Equal("y", Prompts.RestoringOuterWhitespace("x", " y "));
        Assert.Equal("\r\nHei\r\n", Prompts.RestoringOuterWhitespace("\r\nhei\r\n", "Hei"));
    }

    [Fact]
    public void UsesWindowsLineBreaksUnlessTheSelectionHadBareOnes()
    {
        Assert.Equal("Hi all,\r\n\r\n- one\r\n", Prompts.MatchingLineEndings("Hei,\r\n\r\n- en\r\n", "Hi all,\n\n- one\r\n"));
        Assert.Equal("a\r\nb", Prompts.MatchingLineEndings("x", "a\nb"));
        Assert.Equal("a\nb", Prompts.MatchingLineEndings("x\ny", "a\nb"));
    }

    [Fact]
    public void WrapsTextInTags() => Assert.Equal("<text>\nhello\n</text>", Prompts.Wrap("hello"));

    // The prompts are the contract between the two apps; the macOS file is the source of truth.
    [Theory]
    [InlineData("proofread", Prompts.Proofread)]
    [InlineData("translate", Prompts.Translate)]
    [InlineData("custom", Prompts.Custom)]
    public void PromptMatchesMacApp(string name, string prompt) => Assert.Equal(prompt, SwiftString(name));

    [Fact]
    public void PlaceholdersMatchMacApp() =>
        Assert.Contains($"targetPlaceholder = \"{Prompts.TargetPlaceholder}\", secondPlaceholder = \"{Prompts.SecondPlaceholder}\"", SwiftFile());

    private static string SwiftFile([CallerFilePath] string here = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "../../Tekstvask/Prompts.swift"));

    /// <summary>The literal in `static let name = """…"""` in Prompts.swift, with Swift's indentation and line-continuation rules.</summary>
    private static string SwiftString(string name)
    {
        var match = Regex.Match(SwiftFile(), "static let " + name + @" = """"""\n(.*?)\n( *)""""""", RegexOptions.Singleline);
        Assert.True(match.Success, $"no multi-line string for {name} in Prompts.swift");
        string indent = match.Groups[2].Value;
        var lines = match.Groups[1].Value.Split('\n').Select(line => line.StartsWith(indent) ? line[indent.Length..] : line);
        return string.Join("\n", lines).Replace("\\\n", "");
    }
}

/// <summary>A test that needs Windows itself, such as its language recognizer. Skipped when the tests run on macOS.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Needs Windows";
    }
}
