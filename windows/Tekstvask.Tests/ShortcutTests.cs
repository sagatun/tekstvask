namespace Tekstvask.Tests;

public class ShortcutTests
{
    [Fact]
    public void DefaultsAvoidAltGr()
    {
        Assert.Equal("Ctrl+Alt+Shift+L", Shortcut.DefaultProofread.ToString());
        Assert.Equal("Ctrl+Alt+Shift+T", Shortcut.DefaultTranslate.ToString());
        Assert.Null(Shortcut.DefaultProofread.Problem());
    }

    [Theory]
    [InlineData("Ctrl+Alt+Shift+L")]
    [InlineData("Win+Ctrl+F12")]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("Alt+Shift+7")]
    public void FormatsAndParsesBack(string text) => Assert.Equal(text, Shortcut.Parse(text)?.ToString());

    [Fact]
    public void ParsingIgnoresCaseAndSpaces() =>
        Assert.Equal(new Shortcut(Modifiers.Control | Modifiers.Shift, 'K'), Shortcut.Parse("ctrl + shift + k"));

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Hyper+L")]
    [InlineData("Ctrl+Shift+NoSuchKey")]
    public void RejectsUnknownText(string text) => Assert.Null(Shortcut.Parse(text));

    [Theory]
    [InlineData("Ctrl+C")]       // would break copy everywhere
    [InlineData("F5")]
    [InlineData("Ctrl+Alt+E")]   // AltGr+E is € on Nordic keyboards
    public void RejectsShortcutsThatStealTyping(string text) => Assert.NotNull(Shortcut.Parse(text)!.Problem());

    [Fact]
    public void RejectsLayoutDependentKeys() =>
        Assert.NotNull(new Shortcut(Modifiers.Control | Modifiers.Shift, 0xBA).Problem()); // OEM_1 is Ø, ¨ or ; depending on layout
}
