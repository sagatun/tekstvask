import KeyboardShortcuts
import ServiceManagement
import SwiftUI

/// A plain window rather than a SwiftUI `Settings` scene: a menu bar app can then open it from anywhere
/// (first launch, missing API key) and bring it to the front.
enum SettingsWindow {
    private static var window: NSWindow?

    static func show() {
        if window == nil {
            let window = NSWindow(contentViewController: NSHostingController(rootView: SettingsView()))
            window.title = String(localized: "Tekstvask Settings")
            window.isReleasedWhenClosed = false
            window.center()
            self.window = window
        }
        NSApp.activate()
        window?.makeKeyAndOrderFront(nil)
    }
}

struct SettingsView: View {
    @AppStorage("settingsTab") private var tab = "general" // reopens on the last tab, like other Mac settings windows

    var body: some View {
        TabView(selection: $tab) {
            GeneralSettings().tabItem { Text("General") }.tag("general")
            AdvancedSettings().tabItem { Text("Advanced") }.tag("advanced")
        }
        // One fixed size for both tabs: letting the window follow each tab's height sends AppKit into a layout loop.
        .frame(width: 500, height: 710)
    }
}

private struct GeneralSettings: View {
    @State private var apiKey = Keychain.apiKey ?? ""
    @State private var openAtLogin = SMAppService.mainApp.status == .enabled
    @State private var conflict: String?
    @AppStorage(Preferences.targetLanguageKey) private var targetLanguage = Preferences.defaultTargetLanguage
    @AppStorage(Preferences.secondLanguageKey) private var secondLanguage: String?
    @AppStorage(Preferences.customNameKey) private var customName = "" // observed so the custom action's row comes and goes

    var body: some View {
        Form {
            Section {
                SecureField("Anthropic API key", text: $apiKey)
                    .onChange(of: apiKey) { Keychain.apiKey = apiKey.trimmingCharacters(in: .whitespacesAndNewlines) }
                Link("Get an API key at console.anthropic.com", destination: URL(string: "https://console.anthropic.com/settings/keys")!)
            }
            Section("Shortcuts") {
                ForEach(TextAction.available, id: \.self) { action in
                    KeyboardShortcuts.Recorder(for: action.shortcut, onChange: { _ in rejectDuplicate(for: action) }) {
                        Text(action.title)
                    }
                }
                Picker(selection: $targetLanguage) { LanguageOptions() } label: {
                    Text("Target language")
                    let second = Preferences.secondLanguage(stored: secondLanguage, target: targetLanguage)
                    Text("Text already in \(Preferences.languageName(targetLanguage)) is translated to \(Preferences.languageName(second)).")
                }
            }
            Section {
                Toggle("Open at login", isOn: $openAtLogin)
                    .onChange(of: openAtLogin) { setOpenAtLogin(openAtLogin) }
                accessibilityStatus
            } footer: {
                Text("Tekstvask needs Accessibility access to copy and paste the selected text.")
            }
        }
        .formStyle(.grouped)
        .alert(conflict ?? "", isPresented: Binding(get: { conflict != nil }, set: { if !$0 { conflict = nil } })) {}
    }

    private var accessibilityStatus: some View {
        // Polled because macOS posts no notification when access is granted.
        TimelineView(.periodic(from: .now, by: 2)) { _ in
            LabeledContent("Accessibility") {
                if AXIsProcessTrusted() {
                    Label("Granted", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                } else {
                    Button("Open System Settings…") {
                        NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
                    }
                }
            }
        }
    }

    /// The recorder already rejects system and menu shortcuts; this covers our own actions sharing one.
    private func rejectDuplicate(for action: TextAction) {
        guard let other = action.claimShortcut() else { return }
        conflict = String(localized: "That shortcut is already used by “\(other.title)”.")
    }

    private func setOpenAtLogin(_ enabled: Bool) {
        do {
            try enabled ? SMAppService.mainApp.register() : SMAppService.mainApp.unregister()
        } catch {
            openAtLogin = SMAppService.mainApp.status == .enabled
        }
    }
}

private struct AdvancedSettings: View {
    @AppStorage(Preferences.modelKey) private var model: String?
    @AppStorage(Preferences.targetLanguageKey) private var targetLanguage = Preferences.defaultTargetLanguage
    @AppStorage(Preferences.secondLanguageKey) private var secondLanguage: String?
    @AppStorage(Preferences.customNameKey) private var customName = ""
    @State private var editing = TextAction.proofread
    @State private var prompt = TextAction.proofread.prompt

    var body: some View {
        Form {
            Section {
                Picker(selection: Binding(get: { Preferences.model(stored: model) }, set: { model = $0 })) {
                    Text(verbatim: "Claude Haiku 5.5").tag("claude-haiku-5-5")
                    Text(verbatim: "Claude Sonnet 5.5").tag("claude-sonnet-5-5")
                    Text(verbatim: "Claude Opus 5.5").tag("claude-opus-5-5")
                } label: {
                    Text("Model")
                    Text("Haiku is the fastest and cheapest. Sonnet and Opus are slower and cost more, but follow demanding prompts better.")
                }
                Picker(selection: Binding(
                    get: { Preferences.secondLanguage(stored: secondLanguage, target: targetLanguage) },
                    set: { secondLanguage = $0 }
                )) { LanguageOptions() } label: {
                    Text("Second language")
                    Text("Text that’s already in the target language is translated to this language instead.")
                }
            }
            Section("Custom action") {
                TextField(text: $customName, prompt: Text("e.g. Shorten")) {
                    Text("Name in the menu")
                    Text("Name it to add it to the menu and give it a shortcut. Its prompt is under Custom below.")
                }
                .onChange(of: customName) { TextAction.updateCustomShortcut() }
            }
            Section("Prompts") {
                Picker("Prompt", selection: $editing) {
                    Text("Proofread").tag(TextAction.proofread)
                    Text("Translate").tag(TextAction.translate)
                    Text("Custom").tag(TextAction.custom)
                }
                .pickerStyle(.segmented)
                .labelsHidden()
                TextEditor(text: $prompt)
                    .font(.callout)
                    .frame(height: 150)
                    .onChange(of: prompt) { editing.prompt = prompt }
                LabeledContent {
                    Button("Reset to Default") { prompt = editing.defaultPrompt }
                        .disabled(prompt == editing.defaultPrompt)
                } label: {
                    Text("\(Prompts.targetPlaceholder) and \(Prompts.secondPlaceholder) are replaced with the target and second language.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
        }
        .formStyle(.grouped)
        .onChange(of: editing) { prompt = editing.prompt }
    }
}
