import Foundation

/// Settings in UserDefaults. The keys are also used by `@AppStorage` in the views.
enum Preferences {
    static let targetLanguageKey = "targetLanguage"
    static let secondLanguageKey = "secondLanguage"
    static let modelKey = "model"
    static let customNameKey = "customName"
    static let languages = ["en", "nb", "nn", "sv", "da", "fi", "de", "fr", "es", "it", "pt", "nl", "pl", "uk"]

    /// The user's first preferred language, e.g. "de". Not `Locale.current`, which follows the app's own (en/nb/sv) localization.
    static let systemLanguage: String = {
        let code = Locale(identifier: Locale.preferredLanguages.first ?? "en").language.languageCode?.identifier ?? "en"
        return code == "no" ? "nb" : code
    }()

    static let defaultTargetLanguage = systemLanguage == "en" ? "nb" : "en"

    static var targetLanguage: String { UserDefaults.standard.string(forKey: targetLanguageKey) ?? defaultTargetLanguage }

    /// What text that's already in the target language is translated to.
    static var secondLanguage: String {
        secondLanguage(stored: UserDefaults.standard.string(forKey: secondLanguageKey), target: targetLanguage)
    }

    /// The stored choice unless it's the target itself; otherwise the system language, or English (Norwegian if the target is English).
    static func secondLanguage(stored: String?, target: String, system: String = systemLanguage) -> String {
        if let stored, stored != target { return stored }
        if languages.contains(system), system != target { return system }
        return target == "en" ? "nb" : "en"
    }

    static var model: String { model(stored: UserDefaults.standard.string(forKey: modelKey)) }

    /// The stored model if it's one of those offered (older versions let you set any), otherwise the default.
    static func model(stored: String?) -> String {
        if let stored, Claude.models.contains(stored) { return stored }
        return Claude.defaultModel
    }

    /// The custom action's name in the menu; empty while it's unused.
    static var customName: String {
        (UserDefaults.standard.string(forKey: customNameKey) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// The language's name in the app's own UI language, e.g. "engelsk" when the app runs in Norwegian.
    static func languageName(_ code: String) -> String {
        Locale(identifier: Bundle.main.preferredLocalizations.first ?? "en").localizedString(forLanguageCode: code) ?? code
    }

    /// "Engelsk" rather than "engelsk", for pickers and menus.
    static func capitalizedLanguageName(_ code: String) -> String {
        let name = languageName(code)
        return name.prefix(1).uppercased() + name.dropFirst()
    }
}
