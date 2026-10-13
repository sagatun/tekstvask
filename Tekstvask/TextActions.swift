import AppKit
import Carbon.HIToolbox
import KeyboardShortcuts

enum TextAction: String, CaseIterable {
    case proofread, translate, custom

    var shortcut: KeyboardShortcuts.Name {
        switch self {
        case .proofread: .proofread
        case .translate: .translate
        case .custom: .custom
        }
    }

    var title: String {
        switch self {
        case .proofread: String(localized: "Proofread")
        case .translate: String(localized: "Translate to \(Preferences.languageName(Preferences.targetLanguage))")
        case .custom: Preferences.customName
        }
    }

    /// The custom action is in use once it has a name.
    var isAvailable: Bool { self != .custom || !Preferences.customName.isEmpty }

    static var available: [TextAction] { allCases.filter(\.isAvailable) }

    var defaultPrompt: String {
        switch self {
        case .proofread: Prompts.proofread
        case .translate: Prompts.translate
        case .custom: Prompts.custom
        }
    }

    /// UserDefaults key for the user's own prompt; it's stored only while it differs from the default.
    var promptKey: String { "\(rawValue)Prompt" }

    var prompt: String {
        get {
            let stored = UserDefaults.standard.string(forKey: promptKey) ?? ""
            return stored.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? defaultPrompt : stored
        }
        nonmutating set {
            let isDefault = newValue == defaultPrompt || newValue.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            UserDefaults.standard.set(isDefault ? nil : newValue, forKey: promptKey)
        }
    }

    /// Translate is two-way, so its languages depend on the text; the other prompts get the settings as they are.
    func systemPrompt(for text: String) -> String {
        var (target, second) = (Preferences.targetLanguage, Preferences.secondLanguage)
        if self == .translate { (target, second) = Prompts.languages(for: text, target: target, second: second) }
        return Prompts.filling(prompt, target: target, second: second)
    }

    /// A shortcut for an unnamed custom action would still take the keys away from other apps, so it's switched off.
    static func updateCustomShortcut() {
        if TextAction.custom.isAvailable { KeyboardShortcuts.enable(.custom) } else { KeyboardShortcuts.disable(.custom) }
    }

    /// Called after this action got a new shortcut, so that no two actions share one. If a visible action already has it,
    /// this action loses it again and that action is returned. An unnamed custom action gives it up instead.
    func claimShortcut() -> TextAction? {
        guard let keys = KeyboardShortcuts.getShortcut(for: shortcut),
              let holder = TextAction.allCases.first(where: { $0 != self && KeyboardShortcuts.getShortcut(for: $0.shortcut) == keys })
        else { return nil }
        let (loser, keeper) = holder.isAvailable ? (self, holder) : (holder, self)
        KeyboardShortcuts.setShortcut(nil, for: loser.shortcut)
        // The library registers keys, not actions, so removing the loser's shortcut also switched off the keeper's.
        KeyboardShortcuts.enable(keeper.shortcut)
        return holder.isAvailable ? holder : nil
    }
}

/// Copies the selection in the frontmost app, rewrites it with Claude and pastes the result over it.
/// Undo is the target app's own ⌘Z.
@Observable final class TextActions {
    private(set) var isBusy = false
    private(set) var lastError: String?

    init() {
        for action in TextAction.allCases {
            // Key up, so the user's modifier keys are released before we send ⌘C.
            KeyboardShortcuts.onKeyUp(for: action.shortcut) { [weak self] in self?.start(action) }
        }
        TextAction.updateCustomShortcut()
    }

    /// `delay` lets a menu close and focus return to the previous app before ⌘C is sent.
    func start(_ action: TextAction, after delay: Duration = .zero) {
        Task {
            try? await Task.sleep(for: delay)
            await run(action)
        }
    }

    private func run(_ action: TextAction) async {
        guard action.isAvailable else { return }
        guard !isBusy else { return NSSound.beep() }
        guard let apiKey = Keychain.apiKey else { return SettingsWindow.show() }
        // The first time, macOS shows its own prompt; the user presses the shortcut again after granting access.
        guard AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": true] as CFDictionary) else { return }
        isBusy = true
        defer { isBusy = false }

        let clipboard = BorrowedClipboard()
        let app = NSWorkspace.shared.frontmostApplication?.processIdentifier
        guard let original = await clipboard.copySelection() else { return NSSound.beep() }
        do {
            let reply = try await Claude.complete(apiKey: apiKey, model: Preferences.model, system: action.systemPrompt(for: original), text: original)
            let result = Prompts.restoringOuterWhitespace(of: original, in: reply)
            guard NSWorkspace.shared.frontmostApplication?.processIdentifier == app else {
                clipboard.keep(result)
                return report(String(localized: "You switched apps, so the result was copied to the clipboard instead."))
            }
            await clipboard.paste(result)
            lastError = nil
        } catch {
            report(error.localizedDescription)
        }
        clipboard.restore()
    }

    private func report(_ message: String) {
        lastError = message
        let alert = NSAlert()
        alert.messageText = "Tekstvask"
        alert.informativeText = message
        NSApp.activate()
        alert.runModal()
    }
}

/// Borrows the general pasteboard to copy and paste, then puts the user's own clipboard back.
private final class BorrowedClipboard {
    private let pasteboard = NSPasteboard.general
    private let saved: [NSPasteboardItem]
    private var changeCount: Int // the last change we caused; anything newer is the user's

    init() {
        // Deep copy: pasteboard items can't be written back once they've been read.
        // ponytail: reads every type eagerly, so a huge copied file or image is held in memory for a moment.
        saved = (pasteboard.pasteboardItems ?? []).map { item in
            let copy = NSPasteboardItem()
            for type in item.types {
                if let data = item.data(forType: type) { copy.setData(data, forType: type) }
            }
            return copy
        }
        changeCount = pasteboard.changeCount
    }

    /// Sends ⌘C and waits up to a second for the app to copy. Nil when nothing (or only whitespace) is selected.
    func copySelection() async -> String? {
        let before = pasteboard.changeCount
        pressCommand(kVK_ANSI_C)
        for _ in 0..<50 {
            if pasteboard.changeCount != before { break }
            try? await Task.sleep(for: .milliseconds(20))
        }
        guard pasteboard.changeCount != before else { return nil }
        changeCount = pasteboard.changeCount
        guard let text = pasteboard.string(forType: .string), !text.allSatisfy(\.isWhitespace) else {
            restore()
            return nil
        }
        return text
    }

    func paste(_ text: String) async {
        keep(text)
        pressCommand(kVK_ANSI_V)
        try? await Task.sleep(for: .milliseconds(500)) // let the app read the pasteboard before we restore it
    }

    func keep(_ text: String) {
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        changeCount = pasteboard.changeCount
    }

    func restore() {
        guard pasteboard.changeCount == changeCount else { return } // the user copied something new meanwhile; keep it
        pasteboard.clearContents()
        pasteboard.writeObjects(saved)
    }
}

// ponytail: virtual key codes are physical key positions, so plain Dvorak (not "Dvorak – QWERTY ⌘") sends the wrong key.
// Map C/V through the current keyboard layout if someone needs that.
private func pressCommand(_ key: Int) {
    let source = CGEventSource(stateID: .combinedSessionState)
    // Briefly ignore the user's physical keys so modifiers still held from the shortcut don't mix into ⌘C/⌘V.
    source?.setLocalEventsFilterDuringSuppressionState(
        [.permitLocalMouseEvents, .permitSystemDefinedEvents], state: .eventSuppressionStateSuppressionInterval)
    for keyDown in [true, false] {
        let event = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(key), keyDown: keyDown)
        event?.flags = [.maskCommand, CGEventFlags(rawValue: 0x8)] // 0x8 is the left-⌘ device bit, which some apps check
        event?.post(tap: .cgSessionEventTap)
    }
}
