# Mole-Win: Windows system-maintenance app (plan)

## Contents

- [Context](#context)
- [Scope](#scope)
- [Architecture](#architecture)
- [Feature map](#feature-map)
- [Look and feel](#look-and-feel)
- [Security and safety safeguards](#security-and-safety-safeguards)
- [Licensing](#licensing)
- [Project layout](#project-layout)
- [Verification](#verification)
- [Open items and risks](#open-items-and-risks)

## Context

The user decompiled `test.dmg` (macOS app "Mole" 1.16.0 by tw93) with Ghidra
via REA and wants a native Windows equivalent. Mole has an open-source CLI,
`mo`, with a Windows build on the `windows` branch.

The first plan wrapped `mo` as a subprocess. A probe of the Windows build
(local 1.30.0 at commit 3365de0, and upstream tag `V1.31.0-windows`) ruled that
out:

- No JSON mode. `status.exe` and `analyze.exe` are Bubble Tea alt-screen TUIs
  (`cmd/status/main.go`, `cmd/analyze/main.go`); the only flag is
  `analyze -path`.
- Windows `mo` is `mo.cmd` -> `mole.ps1` -> `bin/*.ps1` PowerShell scripts,
  not one `mo.exe`. There is no `installer` command and no `uninstall --list`.
  `uninstall` and `purge` run a ReadKey menu, then `Read-Host "Continue? (y/N)"`.
- `mo clean` has no Claude Code, Codex or Cursor targets, and whitelists
  `~\.ollama\models` (`lib/core/base.ps1`).
- License changed: 1.30.0 is MIT, 1.31.0 is GPL-3.0.

Decision: the engine is all native C#; Mole-Win does not ship or call `mo`.
Delivery is GitHub releases, not commercial yet. Fan and battery control are
dropped so the app needs no elevated access. Consumer name is deferred to the
owner conversation; internal codename stays `Mole-Win`.

Intended outcome: a WPF Fluent app that gives Windows users Mole's Clean,
Apps, AI-tool cleanup, Analyze, read-only Monitor, and keep-awake, with
cleanup rules informed by the MIT-licensed `mo` 1.30.0 rule sets.

## Scope

In: Clean, Apps (uninstall + leftovers), AI-tool cleanup, Analyze (disk
explorer), Monitor (read-only dashboard), keep-awake timer.

Out: fan-speed and battery-limit control (removed to avoid elevation), any
elevated service/broker, cross-platform shell with the Mac app, Microsoft
Store packaging, license-key activation, auto-update infrastructure. These
come only if the project needs them after the owner conversation.

## Architecture

One process, running as the normal user, no elevation:

- **GUI** - WPF + WPF-UI Fluent, built on the user's `wpf-fluent-skill`
  scaffold (.NET 10, CommunityToolkit.Mvvm, Serilog, SQLite/Dapper, resx
  localization, Core/Data/App projects, xUnit v3 tests, WiX v6 MSI). Scaffold
  with that skill's `new-project.bat`.
- **Engine** - native C# services in `MoleWin.Core`, called in-process by the
  view-models. Every destructive service has a scan (preview) step that returns
  items with a reason, and a separate apply step that takes the confirmed list.

Steps that need admin in `mo` (disk optimization, ARP flush, system-wide
clean) are left out of v1 rather than shipping an elevated helper.

## Feature map

All features are native. The `mo` 1.30.0 rule set (MIT) informs which paths
and markers each one uses.

| Feature | Implementation | Rules from `mo` 1.30.0 |
|---------|----------------|------------------------|
| Clean | native scan + apply | `lib/clean/*.ps1` targets (user caches, temp, dev caches, browser caches); default whitelist and protected paths in `lib/core/base.ps1`. |
| Apps | native registry + Windows Apps (Appx) enumeration, run the app's uninstaller, then leftover scan | `bin/uninstall.ps1` leftover locations. |
| Analyze | native folder-size scan; deletions go to the Recycle Bin | none needed. |
| Optimize | native DNS flush and similar non-admin steps only | `bin/optimize.ps1`, minus admin-gated steps. |
| Purge | native project scan for build artifacts | `bin/purge.ps1` project markers (`package.json`, `composer.json`, ...) and artifact folders. |
| Installer leftovers | native scan for installer files in Downloads/Desktop | none (`mo` has no Windows command). |
| Monitor | native performance counters / WMI: CPU, memory, disk, power, network, processes, health score | none. No fan/temp control. |
| AI-tool cleanup | native scanners for Claude Code / Codex / Cursor dirs and Ollama models | none (`mo` does not target them; it whitelists Ollama models). Save a recovery copy before removal, matching Mole. |
| Keep-awake | native `SetThreadExecutionState` | none. |

Safety overlay: removals go to the Recycle Bin or a quarantine folder, never a
permanent delete, and the user sees every item with a reason before apply,
matching the "nothing hard-deleted, shown first" behavior of Mole's own UI.

## Look and feel

Base is WPF-UI Fluent, which is clean but flat on its own. Mole's personality
comes from playful animated mascots and warm art, not its chrome. The DMG ships
those assets (`runner-mole.png`, `runner-rabbit`, `runner-dinosaur`,
`runner-frog`, ... as sprite sheets, plus `earth.jpg`, planet images,
`mole-logo.png`). Reuse that layer on top of Fluent. Laziest-first, all reusing
WPF-UI or Mole's own art, no custom design system:

1. **Depth, not flat:** enable Mica/Acrylic backdrop (WPF-UI supports it on
   Win11) and layer cards with soft shadows.
2. **Motion as the signature:** an animated mascot that runs while a
   scan/clean works (Mole's sprite PNGs animate directly), number count-ups on
   results ("Cleaned 2.3 GB"), hover/press micro-interactions, and WPF-UI's
   built-in `NavigationView` page transitions.
3. **Live data, animated:** Monitor drives an animated ring gauge for the
   health score and sparklines for CPU/memory/disk from the native Monitor
   service, refreshed on a timer, rather than static readouts.
4. **A real palette, not all-gray:** one warm accent plus a proper
   categorical/sequential palette for the storage and monitor charts; light
   and dark via WPF-UI themes. Follow the `dataviz` skill for chart colors.
5. **Personality moments:** empty states and a small completion celebration
   with the mascot, as Mole does.

Confirm with the owner that reusing Mole's mascot/planet art in the Windows app
is acceptable before shipping; otherwise commission equivalents.

## Security and safety safeguards

Mole-Win is a consumer maintenance tool run by the machine's own owner on
their own machine. The design keeps it unambiguously that, and nothing more.

- **Authorized and open.** Built in coordination with the upstream owner
  (tw93), released openly on GitHub with source. No hidden behavior.
- **Local only, no exfiltration.** The app makes no network calls and sends no
  user data anywhere. AI-tool scanning reads local files and reports counts
  in-app; nothing leaves the machine.
- **Non-destructive by default.** Every destructive feature previews first,
  and nothing is removed until the user confirms a list that shows each item
  with a reason. Removals go to the Recycle Bin or quarantine, so they are
  reversible.
- **Protected paths.** Port `mo`'s never-touch set (`C:\Windows`,
  `C:\Program Files`, Defender folders, profile root) and its default
  whitelist, and never add a path that widens what may be removed. AI-tool
  scanners are scoped to known per-tool directories, not free disk traversal.
- **Least privilege.** Runs entirely as the normal user, no elevated service,
  no driver, no kernel component. It reads no credentials, tokens, or secrets;
  AI-tool cleanup targets caches and chat history by path and does not parse or
  transmit their contents.
- **Supply chain.** No third-party binaries are bundled. Never ship anything
  extracted from the decompiled DMG except art that the owner has cleared.
- **Transparent and auditable.** All actions are logged locally (Serilog) so
  the user can see exactly what ran.

## Licensing

- Rules may be ported only from `mo` 1.30.0 or earlier (MIT). Keep its MIT
  notice in `third_party/`. Never copy code or rules from 1.31.0 or later
  (GPL-3.0).
- GUI and engine: MIT or GPL-3.0; decide at scaffold time. Non-commercial
  GitHub releases make this low-risk.
- Do not use `burrow-engine` (FSL-1.1 non-compete bars competing use).
- Reused Mole art (mascots, planets, logo) needs the owner's sign-off.
- Coordinate with the owner (tw93) before any commercial step.

## Project layout

From the `wpf-fluent-skill` scaffold (`new-project.bat`):

- `src/MoleWin.Core/` - native engine (clean, apps, analyze, optimize, purge,
  installer, monitor, AI-tool scanners), protected-path rules, models.
- `src/MoleWin.Data/` - SQLite history/quarantine, numbered migrations.
- `src/MoleWin.App/` - WPF Fluent GUI, view-models, pages (Clean, Apps, AI,
  Analyze, Monitor), mascot/animation assets.
- `tests/` - three xUnit v3 projects.
- `installer/` - WiX v6 MSI. No service, no driver.
- `third_party/` - the MIT notice for rules ported from `mo` 1.30.0.

## Verification

End-to-end on a real Windows 10/11 machine, all as a normal user:

1. Build: `dotnet build -p:Platform=x64` (per the skill's scripts).
2. Clean: run a scan and show the preview in the GUI with reasons, without
   deleting; apply on seeded temp files and confirm they land in the Recycle
   Bin or quarantine.
3. Apps: the Apps page lists registry and Appx apps; uninstall one app in a
   VM and confirm leftover cleanup.
4. AI cleanup: point the native scanner at seeded Claude/Codex/Cursor/Ollama
   dirs; confirm a recovery copy is written before removal.
5. Safety: every apply path refuses protected and whitelisted paths, and no
   apply path deletes permanently.
6. Monitor: the gauge and sparklines refresh on a timer with no elevation
   prompt.
7. Look and feel: mascot animates during a scan; Mica backdrop and page
   transitions render on Win11; light/dark switch works.
8. Tests: xUnit for protected-path rules, each scanner, and quarantine logic.

## Open items and risks

- **Closed: `mo` JSON coverage.** No JSON on the Windows build; replaced by
  the native engine (see [Context](#context)).
- **Closed: AI-tool dir coverage.** `mo clean` does not target them; the native
  scanner is required.
- **Permanent deletes.** The quarantine/Recycle-Bin layer must land before any
  apply path is wired up.
- **Rule drift.** Ported rules freeze at `mo` 1.30.0; later upstream fixes
  (GPL) cannot be copied and must be rederived independently.
- **Unsigned MSI on GitHub builds** will trip SmartScreen/Defender; acceptable
  for a non-commercial release but note it for users. Signing is a
  prerequisite for going commercial.
- **Owner sign-off** on reusing Mole's art before any release.
