# Tekstvask

Proofreads or translates the selected text in any app (Slack, Teams, Mail, Outlook, browsers, Notes, …) with one
shortcut, using Claude. The selection is replaced in place; undo with ⌘Z or Ctrl+Z in that app. For macOS and Windows.

| Action    | macOS | Windows |
|-----------|-------|---------|
| Proofread | ⌃⌥⌘L | Ctrl+Alt+Shift+L |
| Translate | ⌃⌥⌘T | Ctrl+Alt+Shift+T |
| Custom action | none; it appears once it has a name in Settings › Advanced | the same |

Translate is two-way: text that's already in the language you translate to is translated to your own language instead
(by default the system language, so Norwegian ↔ English works without switching). Both are set in Settings › General
as **Translate to** and **Your language**; the language to translate to is also in the menu.

The UI follows the system language: Norwegian Bokmål (and Nynorsk), Swedish, otherwise English.

## Download

| Platform | Download |
|----------|----------|
| macOS 14 or later | [Tekstvask-mac.zip](https://github.com/sagatun/tekstvask/releases/latest/download/Tekstvask-mac.zip) |
| Windows 11, most PCs (Intel/AMD) | [Tekstvask-win-x64.zip](https://github.com/sagatun/tekstvask/releases/latest/download/Tekstvask-win-x64.zip) |
| Windows 11 on ARM (Snapdragon) | [Tekstvask-win-arm64.zip](https://github.com/sagatun/tekstvask/releases/latest/download/Tekstvask-win-arm64.zip) |

Release notes and older versions are on the [Releases](https://github.com/sagatun/tekstvask/releases) page. To hear
about new versions, choose **Watch › Custom › Releases** at the top of the repository page. There is no automatic update.

You need an Anthropic API key (console.anthropic.com › API keys). Use your own key rather than a shared one, so it can
be revoked without affecting anyone else. Anthropic charges the key's account per use.

## Install

### macOS

Unzip `Tekstvask-mac.zip`, drag `Tekstvask.app` to Applications and open it. Then:

1. The Settings window opens. Paste your Anthropic API key. It is stored in the Keychain.
2. Select some text anywhere and press ⌃⌥⌘L. macOS asks for **Accessibility** access. Turn it on for Tekstvask
   (System Settings › Privacy & Security › Accessibility), then press the shortcut again.

Settings are always available from the menu bar icon › Settings…, or by opening Tekstvask again from Finder or
Spotlight (useful when the icon hides behind the notch).

To update, quit Tekstvask from its menu and replace the app in Applications with the new one.

### Windows

Unzip the zip and put `Tekstvask.exe` somewhere permanent, e.g. `%LOCALAPPDATA%\Programs\Tekstvask\`. Double-click
it. It is one self-contained file that needs no .NET install and no admin rights.

The app is not code-signed yet, so the first launch shows SmartScreen's "Windows protected your PC": click
**More info › Run anyway**. Then:

1. The Settings window opens. Paste your Anthropic API key. It is stored encrypted for your Windows user (DPAPI)
   in `%APPDATA%\Tekstvask\api-key.bin`.
2. Select text anywhere and press Ctrl+Alt+Shift+L. Windows needs no permission prompt, unlike macOS' Accessibility.

The icon sits in the notification area. Windows 11 hides new icons behind the ^ arrow; drag it onto the taskbar to keep
it visible. Click it for the menu (actions, target language, Settings…, Quit). Starting `Tekstvask.exe` again opens
Settings.

To update, quit Tekstvask from its menu and replace `Tekstvask.exe` with the new one.

### Settings

- **General:** API key, shortcuts, Translate to and Your language, open at login.
- **Advanced:** the model (Haiku 5.5 is the default and fastest; Sonnet 5.5 and Opus 5.5 are slower and cost more),
  the custom action's name, and the three prompts. Each prompt can be edited and reset to its default; in a prompt,
  `{target}` becomes the language to translate to and `{second}` the other one. Only prompts that differ from the
  default are stored, so improved defaults in a new version reach everyone who hasn't edited theirs.

## Build from source

Requires Xcode 26+ and XcodeGen. The Xcode project is generated from `project.yml` and is not checked in.

```sh
brew install xcodegen
make test      # unit tests
make run       # ad-hoc signed build in build/Tekstvask.app, then launch it
make install   # copy to /Applications
```

Ad-hoc builds get a new code signature every time, so macOS forgets the Accessibility grant (and may ask for
Keychain access) after each rebuild. Developer ID builds keep it.

## Release

The Mac app is signed with Developer ID and notarized. The Windows app is not signed yet
(see [Release (Windows)](#release-windows-when-its-a-hit)).

One-time setup on the Mac that builds releases:

1. Install a **Developer ID Application** certificate in the login keychain (Xcode › Settings › Accounts ›
   Manage Certificates › + › Developer ID Application).
2. Store notarization credentials:
   `xcrun notarytool store-credentials tekstvask-notary --apple-id <apple id> --team-id <TEAM_ID>`
   (use an app-specific password from account.apple.com).

Then bump the version in `project.yml` (`MARKETING_VERSION`) and `windows/Tekstvask/Tekstvask.csproj` (`Version`), and:

```sh
make release TEAM_ID=<TEAM_ID>   # build/Tekstvask-mac.zip: signed, notarized and stapled
make windows                      # build/Tekstvask-win-x64.zip and build/Tekstvask-win-arm64.zip
gh release create v1.0.0 build/Tekstvask-mac.zip build/Tekstvask-win-x64.zip build/Tekstvask-win-arm64.zip \
  --title "Tekstvask 1.0.0" --notes-file <release-notes.md>
```

The links under [Download](#download) always point to the latest release, so the README needs no change.

## How it works

The shortcut sends ⌘C to the frontmost app and reads the pasteboard. The text goes to the Messages API with a
proofread or translate system prompt (`Tekstvask/Prompts.swift`). For Translate, Apple's NaturalLanguage
framework first recognizes the text's language: if it's clearly the language to translate to, the prompt is filled
with the two languages swapped, so it asks for your own language first. The reply is pasted with ⌘V, and the user's own
clipboard is restored afterwards. The clipboard is left alone if the user copied something new in the meantime.

Shortcut conflicts: the recorder in Settings rejects shortcuts taken by macOS or by the app's menu
(from [KeyboardShortcuts](https://github.com/sindresorhus/KeyboardShortcuts)), and Tekstvask rejects giving both
actions the same shortcut. macOS has no API to see shortcuts registered by other third-party apps (Raycast,
Alfred, …), so those clashes are not detected.

## Manual test checklist

Run before a release. Automated tests cover the API contract and text handling only.

1. In Slack, TextEdit, a Safari text area, Notes and Mail: select text, press ⌃⌥⌘L. The text is replaced and ⌘Z restores it.
2. With Translate to English and Your language Norwegian: an English sentence becomes Norwegian, a Norwegian one
   English, and a single word like "ok" still translates.
3. Advanced: each model proofreads without an error. Naming the custom action adds it to the menu and Settings, where it
   can get a shortcut; clearing the name removes both. An edited prompt takes effect at once; Reset to Default undoes it.
4. The clipboard holds what it held before.
5. No selection: a beep and nothing changes.
6. A wrong API key gives an alert saying the key was rejected.
7. Settings: recording ⌘C, ⌘Space or ⌘Tab is refused with an explanation. Giving two actions the same shortcut is
   refused, and the other action's shortcut keeps working. A new shortcut works at once.
8. With the system language set to Norwegian or Swedish, the menu and Settings are translated.
9. A release zip opens on another Mac without a Gatekeeper warning.

## Decisions

Choices made during implementation where the plan left room or turned out simpler another way:

- **Settings is a plain window, not a SwiftUI `Settings` scene.** A menu bar app can then open it on first launch or when the key is missing, and bring it to the front.
- **Errors are shown as an alert,** and the latest error is also shown in the menu. Notifications would need a permission prompt.
- **A reply cut off by `max_tokens` is an error.** The selection is never replaced with half a text.
- **Release builds are signed directly with Developer ID** (`make release`), without an archive/export step or `ExportOptions.plist`.
- **The shortcut is shown as text in the menu item title.** The library doesn't expose a SwiftUI key equivalent, and a real one would also fire while the menu is open.
- **Each model gets the least thinking it allows, at effort `low`,** for the lowest latency: Haiku 5.5 sends
  `thinking: disabled`, Sonnet 5.5 `between_tools` (it rejects `disabled`), and Opus 5.5 leaves `thinking` out (it always
  thinks). None of them accept sampling parameters.
- **Sonnet and Opus send `fallbacks: "default"`** (beta header `server-side-fallback-2026-07-01`): if the model declines
  a text, the API re-runs it on a model Anthropic picks instead of returning a refusal. Haiku has no server-side fallback.
- **The custom action is one action, not a list.** It stays out of the menu until it has a name, and its shortcut is
  switched off meanwhile, so it never takes keys from other apps. It keeps that shortcut for when it's named again,
  unless another action is given the same keys in the meantime.
- **After a duplicate shortcut is removed, the action that keeps it registers it again.** KeyboardShortcuts registers
  keys, not actions, so clearing one action's shortcut also switched off another action with the same keys.
- **Translate decides the two-way direction itself** (`Prompts.languages(for:)`): the platform's language recognizer
  names the text's language, and text in the language to translate to gets the two languages swapped in the prompt.
  Haiku without thinking often ignored the prompt's "if it is already in {target}" clause and returned English text
  as English. The prompt text is unchanged, so both apps still send the same prompt; the clause covers text too short
  to recognize, such as "ok".
- **The Settings window has one fixed size for both tabs.** Letting the window follow each tab's height sent AppKit into
  a layout loop that crashed the app.

## Known limitations

- KeyboardShortcuts has no Norwegian or Swedish translation, so its conflict warnings appear in English.
- On a plain Dvorak layout the simulated ⌘C/⌘V hit the wrong keys (they use physical key positions).
- Password fields block simulated copy, which gives the "no selection" beep.
- Haiku's safety filter can occasionally decline harmless text. This is shown as an error.

## Windows

A separate C#/.NET 10 app in `windows/`, not a shared cross-platform codebase: the hard parts are OS-specific on both
platforms. What the two apps share is a contract. The default prompts are copied verbatim (a test compares
`windows/Tekstvask/Prompts.cs` with `Prompts.swift`), and the request parameters per model match `Claude.swift` (also
tested). The Windows app calls the API through the official `Anthropic` NuGet SDK, on its beta path
(`client.Beta.Messages`) because `fallbacks` is a beta parameter. Settings has the same General and Advanced tabs.
Installing it is described under [Install › Windows](#windows).

The shortcuts add Shift because Ctrl+Alt+letter is AltGr on Nordic keyboards.

### Build (Windows)

Requires the .NET 10 SDK, on macOS or Windows.

```sh
make windows-test   # unit tests (run on macOS too)
make windows        # build/Tekstvask-win-x64.zip and build/Tekstvask-win-arm64.zip
```

On Windows without make: `dotnet publish windows/Tekstvask -c Release -r win-x64 -o build/win-x64`.

### How it works (Windows)

| macOS | Windows |
|---|---|
| `MenuBarExtra` | WinForms `NotifyIcon` with a WPF menu (Fluent theme) |
| KeyboardShortcuts recorder | `RegisterHotKey`, which fails if another app holds the shortcut, so conflicts are reported |
| `CGEvent` ⌘C/⌘V + `NSPasteboard.changeCount` | `SendInput` Ctrl+C/Ctrl+V + `GetClipboardSequenceNumber` |
| Keychain | DPAPI (`ProtectedData`) |
| `URLSession` | the official `Anthropic` NuGet SDK |
| `SMAppService` | the `HKCU\…\Run` registry value |
| SwiftUI Settings form | WPF window with the Windows 11 (Fluent) theme, light/dark from the system |
| NaturalLanguage (`NLLanguageRecognizer`), swapped from 60 % confidence | Extended Linguistic Services (`elscore.dll`), which gives no answer for very short text; it takes Danish for Norwegian Bokmål |

A hotkey fires on key down, while the user still holds Ctrl+Alt+Shift. The app waits for the modifiers to be released
before it sends Ctrl+C. The macOS app acts on key up for the same reason. It also sends a no-op key so that releasing
Alt doesn't open the target app's menu bar.

The clipboard is saved format by format and put back afterwards, unless the user copied something new in the meantime.
The text Tekstvask puts on the clipboard briefly is kept out of Win+V history and cloud clipboard sync.

### Decisions (Windows)

- **WPF + Fluent theme for the UI, WinForms only for the tray icon.** WPF has no tray icon. The Fluent theme is still
  marked experimental in .NET 10 (warning WPF0001, suppressed).
- **The tray menu remembers the previous foreground window.** Clicking a tray icon makes the taskbar the foreground
  window, so Proofread/Translate from the menu first gives focus back to the window the user came from.
- **Errors are a topmost message box,** as on macOS, and the latest one is also shown in the menu. "You switched apps,
  so the result was copied" is a notification instead: nothing went wrong, and a message box would pull the user back.
- **A small "Working…" label follows the pointer while Claude works.** Windows 11 hides new tray icons behind the ^
  arrow, so the busy icon alone is easy to miss. The label never takes focus and lets clicks through.
- **The Settings window follows each tab's height but stays on screen;** on a small screen (1080p at 150 %) a tab
  scrolls instead.
- **One self-contained single-file `.exe` per architecture** (about 70 MB, 65 MB zipped), not an installer.
  Users need no .NET runtime. A framework-dependent build would be about 2 MB but would need the .NET 10 Desktop Runtime.
- **A request times out after 120 seconds and is retried once.** The SDK's default is two retries, also after a
  timeout, and each attempt is paid for. HttpClient's own 100-second limit is turned off so the SDK's timeout is the
  one that applies.
- **The settings window ignores Alt's menu activation.** The shortcut recorder swallows the key pressed with Alt, so
  Windows otherwise took the Alt release for a lone Alt tap, and menu mode ate the next click.
- **Shortcuts need two modifiers**, so they can't steal ordinary app shortcuts such as Ctrl+C, and **Ctrl+Alt alone
  is refused**, because that's AltGr. Only letters, digits, F-keys and navigation keys are allowed: punctuation keys
  sit in different places on different layouts.

### Known limitations (Windows)

- Not code-signed: SmartScreen warns on first launch (see Release below).
- Windows blocks simulated keys into apps running as administrator, so Tekstvask can't copy from or paste into those.
- A shortcut another app already holds never reaches the recorder, because Windows delivers it to that app. The recorder
  just keeps waiting. A conflict with a saved shortcut is reported at startup, in the menu.
- Tested on Windows 11 ARM64 (and the x64 build under emulation) in Parallels, not yet on physical x64 PCs or Windows 10.

### Release (Windows), when it's a hit

Sign `Tekstvask.exe` so SmartScreen stops warning. The cheapest route is
[Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) (about $10/month; needs identity
verification). Then sign each `.exe` with `signtool sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 …`
before zipping. Nothing in the code changes.

### Manual test checklist (Windows)

1. In Notepad, Word, Outlook, Edge/Chrome text fields and Teams/Slack: select text, press Ctrl+Alt+Shift+L.
   The text is replaced and Ctrl+Z restores it.
2. The clipboard holds what it held before.
3. No selection: a beep and nothing changes.
4. A wrong API key gives a message box saying the key was rejected.
5. Proofread from the tray menu works on the window you came from.
6. Settings: Ctrl+C or Ctrl+Alt+E is refused with an explanation, giving both actions the same shortcut is refused,
   and a new shortcut works at once.
7. The same Advanced checks as on macOS (models, custom action, prompts, two-way translate). Switching apps while
   Claude works shows a notification and leaves the result on the clipboard.
8. With Windows' display language set to Norwegian or Swedish, the menu and Settings are translated.
