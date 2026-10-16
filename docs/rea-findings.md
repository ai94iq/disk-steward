# REA findings: test.dmg (Mole 1.16.0)

## Contents

- [What it is](#what-it-is)
- [Bundle contents](#bundle-contents)
- [What the app does](#what-the-app-does)
- [Privileged helper](#privileged-helper)
- [How this was analyzed](#how-this-was-analyzed)

## What it is

`test.dmg` is the macOS app **Mole 1.16.0** (build 324) by tw93, bundle id
`com.tw93.MoleApp`. It is a system cleanup and maintenance app. Findings below
come from static analysis only; nothing was run.

- Universal binary: x86_64 and arm64, Swift.
- Requires macOS 14 or later; built with the macOS 27 SDK.
- Source revision recorded in Info.plist: `69befc331671396345794427a9053d05b7a67218`.
- Signed with Developer ID "wei tang (5EH69Y5X38)" (read from strings; signature
  not verified on this host).
- Registers the `mole://` URL scheme.
- Sparkle auto-updater, feed `https://mole.fit/appcast.xml`,
  `SUPublicEDKey` present; auto-check on, auto-install off.

## Bundle contents

- `Mole.app/Contents/MacOS/Mole` - main app (universal).
- `Mole.app/Contents/Library/LaunchServices/com.tw93.MoleApp.systemhelper` -
  privileged helper installed via `SMPrivilegedExecutables`.
- `Mole.app/Contents/Frameworks/Sparkle.framework` - updater, with Updater.app
  and Downloader/Installer XPC services.
- Resources: animated runner sprites (`runner-mole.png`, `runner-rabbit`,
  `runner-dinosaur`, `runner-frog`, ...), planet art (`earth.jpg`, `jupiter.jpg`,
  `mars.png`, ...), `mole-logo.png`, `mole.icns`, localized strings for many
  languages.

## What the app does

From 1533 UI strings and the Info.plist usage descriptions:

- Cleans caches, old builds, and installer leftovers in Desktop, Documents, and
  Downloads.
- Uninstalls apps and removes the login items they leave behind (Apple Events
  usage description).
- "AI Tool Cleanup and Care": deletes old Claude Code, Codex, and Cursor chats
  and finished worktrees, removes unused Ollama models, compacts their
  databases. Strings say it saves a recovery copy or moves files to Trash
  before removing.
- A system monitor showing camera, mic, and GPU use, plus a keep-awake timer
  and animated menu-bar runner icons.

## Privileged helper

`com.tw93.MoleApp.systemhelper` runs with admin rights and exposes an XPC
service (`FanHelperProtocol`):

- `applyFanWithMode:targetPercent:` - sets fan speed; writes SMC keys `FNum`,
  `Ftst`.
- `applyBatteryChargingWithEnabled:` - sets a battery charge limit; writes SMC
  keys `CH0B`, `CH0C`, `CHTE`.
- `beginLowLatencyWithInterfaceName:seconds:` / `endLowLatencyForRemoval:` -
  disables AWDL for a set time to cut Wi-Fi latency; a lease file at
  `/private/var/run/com.tw93.MoleApp.awdl-lease` lets it restore AWDL after a
  crash, with retries.
- `versionWithReply:` - reports helper version.

Caller authentication: it takes the client PID, resolves it with
`SecCodeCopyGuestWithAttributes`, builds a requirement with
`SecRequirementCreateWithString`, and checks it with `SecCodeCheckValidity`
against team `5EH69Y5X38`; it also sets `setCodeSigningRequirement` on the
connection and logs `rejected untrusted XPC peer pid=%d`. No shell execution or
file deletion was found in the helper.

## How this was analyzed

- `test.dmg` is an Apple DMG v4. REA has no DMG reader and its plist/signature/
  Mach-O commands are macOS-only, so the DMG was extracted with 7-Zip into an
  isolated folder and REA's cross-platform commands were run on the unpacked
  app.
- REA `trace-dylib-resolution` confirmed the Mach-O layout (universal, 12 root
  images across the app, helper, and Sparkle).
- The x86_64 slices of `Mole` and the helper were carved out and decompiled
  with Ghidra 12.1.4 headless (JDK 25) on Windows; the helper produced 378
  functions, read for the XPC surface and SMC keys above.
