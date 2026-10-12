using System.Globalization;

namespace Tekstvask;

/// <summary>
/// UI text in English, Norwegian Bokmål and Swedish, keyed by the English text like Localizable.xcstrings.
/// The UI follows the Windows display language: Norwegian (Bokmål or Nynorsk) gets Bokmål, Swedish gets Swedish,
/// anything else English.
/// </summary>
public static class Strings
{
    /// <summary>The Windows display language at startup, e.g. "nb", "de".</summary>
    public static readonly string SystemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch { "no" => "nb", var code => code };

    public static string Language { get; set; } = SystemLanguage switch
    {
        "nb" or "nn" => "nb",
        "sv" => "sv",
        _ => "en",
    };

    private static readonly Dictionary<string, (string Nb, string Sv)> Table = new()
    {
        // Shared with the macOS app (Localizable.xcstrings).
        ["Anthropic API error ({0}): {1}"] = ("Feil fra Anthropic API ({0}): {1}", "Fel från Anthropic API ({0}): {1}"),
        ["Anthropic API key"] = ("Anthropic API-nøkkel", "Anthropic API-nyckel"),
        ["Claude declined to process this text."] = ("Claude avviste å behandle denne teksten.", "Claude avböjde att bearbeta texten."),
        ["Claude returned no text."] = ("Claude returnerte ingen tekst.", "Claude returnerade ingen text."),
        ["Get an API key at console.anthropic.com"] = ("Skaff en API-nøkkel på console.anthropic.com", "Skaffa en API-nyckel på console.anthropic.com"),
        ["Open at login"] = ("Åpne ved pålogging", "Öppna vid inloggning"),
        ["Proofread"] = ("Språkvask", "Korrekturläs"),
        ["Quit Tekstvask"] = ("Avslutt Tekstvask", "Avsluta Tekstvask"),
        ["Settings…"] = ("Innstillinger…", "Inställningar…"),
        ["Shortcuts"] = ("Hurtigtaster", "Kortkommandon"),
        ["Target language"] = ("Målspråk", "Målspråk"),
        ["Tekstvask Settings"] = ("Innstillinger for Tekstvask", "Inställningar för Tekstvask"),
        ["That shortcut is already used by “{0}”."] = ("Hurtigtasten brukes allerede av «{0}».", "Kortkommandot används redan av ”{0}”."),
        ["The API key was rejected. Check it in Settings."] = ("API-nøkkelen ble avvist. Sjekk den i Innstillinger.", "API-nyckeln avvisades. Kontrollera den i Inställningar."),
        ["The text is too long. Select less text and try again."] = ("Teksten er for lang. Marker mindre tekst og prøv igjen.", "Texten är för lång. Markera mindre text och försök igen."),
        ["Translate to {0}"] = ("Oversett til {0}", "Översätt till {0}"),
        ["You switched apps, so the result was copied to the clipboard instead."] = ("Du byttet app, så resultatet ble kopiert til utklippstavlen i stedet.", "Du bytte app, så resultatet kopierades till urklipp i stället."),
        ["General"] = ("Generelt", "Allmänt"),
        ["Advanced"] = ("Avansert", "Avancerat"),
        ["Text already in {0} is translated to {1}."] = ("Tekst som allerede er på {0}, oversettes til {1}.", "Text som redan är på {0} översätts till {1}."),
        ["Model"] = ("Modell", "Modell"),
        ["Haiku is the fastest and cheapest. Sonnet and Opus are slower and cost more, but follow demanding prompts better."] =
            ("Haiku er raskest og billigst. Sonnet og Opus er tregere og koster mer, men følger krevende prompter bedre.",
             "Haiku är snabbast och billigast. Sonnet och Opus är långsammare och kostar mer, men följer krävande prompter bättre."),
        ["Second language"] = ("Andrespråk", "Andraspråk"),
        ["Text that’s already in the target language is translated to this language instead."] =
            ("Tekst som allerede er på målspråket, oversettes til dette språket i stedet.",
             "Text som redan är på målspråket översätts till det här språket i stället."),
        ["Custom action"] = ("Egen handling", "Egen åtgärd"),
        ["Name in the menu"] = ("Navn i menyen", "Namn i menyn"),
        ["e.g. Shorten"] = ("f.eks. Forkort", "t.ex. Korta ner"),
        ["Name it to add it to the menu and give it a shortcut. Its prompt is under Custom below."] =
            ("Gi den et navn for å få den i menyen og gi den en hurtigtast. Prompten ligger under Egen nedenfor.",
             "Ge den ett namn för att få den i menyn och ge den ett kortkommando. Prompten finns under Egen nedan."),
        ["Prompts"] = ("Prompter", "Prompter"),
        ["Translate"] = ("Oversett", "Översätt"),
        ["Custom"] = ("Egen", "Egen"),
        ["Reset to Default"] = ("Tilbakestill", "Återställ"),
        ["{0} and {1} are replaced with the target and second language."] =
            ("{0} og {1} byttes ut med målspråket og andrespråket.", "{0} och {1} ersätts med målspråket och andraspråket."),

        // Windows only.
        ["Type a shortcut…"] = ("Trykk en hurtigtast…", "Tryck ett kortkommando…"),
        ["Remove shortcut"] = ("Fjern hurtigtast", "Ta bort kortkommando"),
        ["Use at least two of Ctrl, Alt, Shift and Win together with a letter, digit or function key."] =
            ("Bruk minst to av Ctrl, Alt, Shift og Win sammen med en bokstav, et tall eller en funksjonstast.",
             "Använd minst två av Ctrl, Alt, Shift och Win tillsammans med en bokstav, en siffra eller en funktionstangent."),
        ["Ctrl+Alt is AltGr on many keyboards. Add Shift or Win."] =
            ("Ctrl+Alt er AltGr på mange tastaturer. Legg til Shift eller Win.", "Ctrl+Alt är AltGr på många tangentbord. Lägg till Shift eller Win."),
        ["That shortcut is taken by Windows or another app."] =
            ("Hurtigtasten er allerede i bruk av Windows eller en annen app.", "Kortkommandot används redan av Windows eller en annan app."),
        ["{0} is taken by Windows or another app. Choose another shortcut in Settings."] =
            ("{0} er allerede i bruk av Windows eller en annen app. Velg en annen hurtigtast i Innstillinger.",
             "{0} används redan av Windows eller en annan app. Välj ett annat kortkommando i Inställningar."),
        ["Tekstvask runs in the notification area. Select text in any app and press {0}."] =
            ("Tekstvask kjører i systemstatusfeltet. Marker tekst i en hvilken som helst app og trykk {0}.",
             "Tekstvask körs i meddelandefältet. Markera text i valfri app och tryck {0}."),
        ["Working…"] = ("Jobber…", "Arbetar…"),
        ["Claude took too long to answer. Try again, or select less text."] =
            ("Claude brukte for lang tid på å svare. Prøv igjen, eller marker mindre tekst.",
             "Claude tog för lång tid på sig att svara. Försök igen, eller markera mindre text."),
    };

    /// <summary>The text in the UI language. Unknown keys fall back to English.</summary>
    public static string T(string english) =>
        Language != "en" && Table.TryGetValue(english, out var t) ? (Language == "nb" ? t.Nb : t.Sv) : english;

    public static string T(string english, params object[] args) => string.Format(T(english), args);

    // Language names as CLDR (and so macOS) spells them; Windows' own names depend on its display language.
    private static readonly Dictionary<string, (string En, string Nb, string Sv)> LanguageNames = new()
    {
        ["en"] = ("English", "engelsk", "engelska"),
        ["nb"] = ("Norwegian Bokmål", "norsk bokmål", "norskt bokmål"),
        ["nn"] = ("Norwegian Nynorsk", "norsk nynorsk", "nynorska"),
        ["sv"] = ("Swedish", "svensk", "svenska"),
        ["da"] = ("Danish", "dansk", "danska"),
        ["fi"] = ("Finnish", "finsk", "finska"),
        ["de"] = ("German", "tysk", "tyska"),
        ["fr"] = ("French", "fransk", "franska"),
        ["es"] = ("Spanish", "spansk", "spanska"),
        ["it"] = ("Italian", "italiensk", "italienska"),
        ["pt"] = ("Portuguese", "portugisisk", "portugisiska"),
        ["nl"] = ("Dutch", "nederlandsk", "nederländska"),
        ["pl"] = ("Polish", "polsk", "polska"),
        ["uk"] = ("Ukrainian", "ukrainsk", "ukrainska"),
    };

    public static IEnumerable<string> LanguageCodes => LanguageNames.Keys;

    /// <summary>
    /// What text that's already in the target language is translated to: the stored choice unless it's the target itself;
    /// otherwise the system language, or English (Norwegian if the target is English). Same rule as Preferences.swift.
    /// </summary>
    public static string SecondLanguage(string? stored, string target, string? system = null)
    {
        system ??= SystemLanguage;
        if (stored != null && stored != target) return stored;
        if (LanguageNames.ContainsKey(system) && system != target) return system;
        return target == "en" ? "nb" : "en";
    }

    /// <summary>For the prompt, which always names the language in English.</summary>
    public static string EnglishLanguageName(string code) => LanguageNames.TryGetValue(code, out var n) ? n.En : code;

    /// <summary>The language's name in the UI language, e.g. "engelsk" when the UI is Norwegian.</summary>
    public static string LanguageName(string code) =>
        !LanguageNames.TryGetValue(code, out var n) ? code : Language switch { "nb" => n.Nb, "sv" => n.Sv, _ => n.En };
}
