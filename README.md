# Mole-Win

Windows system-maintenance app (internal codename `Mole-Win`; consumer name
TBD). A native WPF Fluent app with its own C# engine that gives Windows users
cleanup, app uninstall, AI-tool cleanup, disk analysis, and a read-only system
monitor.

Status: Clean (with AI-tool cleanup), Apps, Analyze, Monitor and keep-awake
work; see [Next steps](#next-steps) for what is left.

## Contents

- [What this is](#what-this-is)
- [What works](#what-works)
- [Build and run](#build-and-run)
- [Docs](#docs)
- [Next steps](#next-steps)

## What this is

A Windows counterpart to tw93's macOS app "Mole". The engine is native C#;
cleanup rules are ported from the MIT-licensed `mo` 1.30.0 Windows build (the
Windows `mo` has no JSON or non-interactive mode, so wrapping it was dropped).
Runs entirely as the normal user, no elevation. See `docs/plan.md` for the
full design and `docs/rea-findings.md` for the analysis of the original app
that informed it.

## What works

Nothing is ever removed without ticking it and confirming, and removal only
ever goes to the Recycle Bin.

- **Monitor** (start page): health, CPU and memory as ring gauges, one-minute CPU
  and network charts, drive capacity bars and top processes, sampled every 2 s
  from plain Windows APIs while the page is open. A Keep awake picker holds off
  sleep for a set time or until turned off.
- **Clean:** scans on open, about a second. 86 cache locations grouped into
  collapsible system, browser, app, graphics driver, developer tool and AI tool
  cards (old Claude Code chats and Codex sessions, Cursor caches; Claude Code
  memory is never touched). Rules and the default whitelist come from `mo`
  1.30.0 (MIT, see `third_party/`). The scan log is under Show details.
- **Apps:** installed apps with their icons and install dates, sorted by name,
  size or install date like Windows Settings, with search. Uninstall runs the
  app's own uninstaller, waits until Windows no longer lists it, then shows
  exact-name leftovers unticked. Store apps are not listed yet.
- **Analyze:** a tree of drives and folders. Drives show used space against
  capacity and only expand; folders list at once and measure in the background.
  Sizes are space on disk. Protected folders and system files (pagefile,
  hiberfil, swapfile and anything with the System attribute) cannot be ticked.
- **Safety:** every removal goes through `SafeRemover`, which re-checks
  `ProtectedPaths` (Windows, Program Files, ProgramData, drive roots, the
  profile and its known folders) and only recycles. Drives without a Recycle
  Bin are refused rather than deleted from.

## Build and run

Needs Windows 10 22H2 or 11 (x64) and the .NET 10 SDK pinned in `global.json`.

| Script | Purpose |
|---|---|
| `run.bat` | Run the app in Debug |
| `build.bat` | Publish `MoleWin.exe` to `artifacts\publish\win-x64` |
| `test.bat` | Run all tests (the pre-commit hook runs them too) |
| `package.bat` | Test, build and create the MSI in `artifacts\installer` |

Pass `--no-pause` to skip the final key press.

## Docs

- `docs/plan.md` - architecture, scope, feature map, look and feel, safeguards,
  licensing, verification, and open items.
- `docs/rea-findings.md` - static analysis of the original macOS Mole app.
- `docs/architecture.md`, `docs/localization.md`, `docs/release.md` - from the
  wpf-fluent scaffold.

## Next steps

1. Your review of the running app (`run.bat`, or `artifacts\publish\win-x64\MoleWin.exe`
   after `build.bat`).
2. Store (Appx) apps on the Apps page: needs the WinRT target
   `net10.0-windows10.0.19041.0`, an ADR under the wpf-fluent skill rules.
3. Ollama models: listed by `mo` as whitelisted; removing them safely needs
   `ollama rm`, which deletes permanently, so it is left out for now.
4. Owner sign-off before reusing any Mole art; consumer name.
