import KeyboardShortcuts
import SwiftUI

extension KeyboardShortcuts.Name {
    // ⌃⌥⌘ + letter is not taken by any default macOS shortcut.
    static let proofread = Self("proofread", default: .init(.l, modifiers: [.control, .option, .command]))
    static let translate = Self("translate", default: .init(.t, modifiers: [.control, .option, .command]))
    static let custom = Self("custom")
}

@main
struct TekstvaskApp: App {
    @NSApplicationDelegateAdaptor private var delegate: AppDelegate
    @State private var actions = TextActions()

    init() {
        if Keychain.apiKey == nil { Task { SettingsWindow.show() } }
    }

    var body: some Scene {
        MenuBarExtra {
            MenuView(actions: actions)
        } label: {
            Image(systemName: actions.isBusy ? "ellipsis.circle" : "text.badge.checkmark")
        }
    }
}

/// Opening the app again (Finder, Spotlight) shows Settings, since the menu bar icon can be hidden behind the notch.
final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows: Bool) -> Bool {
        SettingsWindow.show()
        return false
    }
}
