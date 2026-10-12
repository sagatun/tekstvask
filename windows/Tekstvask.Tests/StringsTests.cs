using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Tekstvask.Tests;

public class StringsTests
{
    /// <summary>Every UI text in the code and XAML has a Norwegian and Swedish translation, so none shows up in English.</summary>
    [Fact]
    public void EveryUiTextIsTranslated()
    {
        var table = (IDictionary)typeof(Strings).GetField("Table", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        string folder = Path.Combine(Path.GetDirectoryName(Here())!, "../Tekstvask");
        var keys = Directory.GetFiles(folder, "*.cs").SelectMany(file => Regex.Matches(File.ReadAllText(file), @"Strings\.T\(""([^""]+)""").Select(m => m.Groups[1].Value))
            .Concat(Directory.GetFiles(folder, "*.xaml").SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{local:Tr '([^']+)'\}").Select(m => m.Groups[1].Value)))
            .Distinct().ToList();
        Assert.True(keys.Count > 30, $"found only {keys.Count} texts; is the pattern still right?");
        var untranslated = keys.Where(key => !table.Contains(key)).ToList();
        Assert.True(untranslated.Count == 0, "not in Strings.Table: " + string.Join(" | ", untranslated));
    }

    private static string Here([CallerFilePath] string path = "") => path;
}
