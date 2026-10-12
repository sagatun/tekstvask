import KeyboardShortcuts
import SwiftUI

struct MenuView: View {
    let actions: TextActions
    // Observed so the items refresh when the language, the custom action or a shortcut changes; the shortcut keys are the library's.
    @AppStorage(Preferences.targetLanguageKey) private var targetLanguage = Preferences.defaultTargetLanguage
    @AppStorage(Preferences.customNameKey) private var customName = ""
    @AppStorage("KeyboardShortcuts_proofread") private var proofreadShortcut: String?
    @AppStorage("KeyboardShortcuts_translate") private var translateShortcut: String?
    @AppStorage("KeyboardShortcuts_custom") private var customShortcut: String?

    var body: some View {
        ForEach(TextAction.available, id: \.self) { action in
            Button(title(for: action)) { actions.start(action, after: .milliseconds(200)) }
        }
        Picker("Target language", selection: $targetLanguage) { LanguageOptions() }
        Divider()
        if let error = actions.lastError {
            Text(error)
            Divider()
        }
        Button("Settings…") { SettingsWindow.show() }
            .keyboardShortcut(",")
        Button("Quit Tekstvask") { NSApp.terminate(nil) }
            .keyboardShortcut("q")
    }

    private func title(for action: TextAction) -> String {
        guard let shortcut = KeyboardShortcuts.getShortcut(for: action.shortcut) else { return action.title }
        return "\(action.title)    \(shortcut)"
    }
}

/// The languages Tekstvask offers, as picker items tagged with their code.
struct LanguageOptions: View {
    var body: some View {
        ForEach(Preferences.languages, id: \.self) { code in
            Text(verbatim: Preferences.capitalizedLanguageName(code)).tag(code)
        }
    }
}
