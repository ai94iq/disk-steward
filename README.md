# Mole-Win

Windows system-maintenance app (internal codename `Mole-Win`; consumer name
TBD). A native WPF Fluent app with its own C# engine that gives Windows users
cleanup, app uninstall, AI-tool cleanup, disk analysis, and a read-only system
monitor.

Status: Analyze and Monitor work; Clean, Apps, AI-tool cleanup and keep-awake
are not built yet (see [Next steps](#next-steps)).

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

- **Analyze:** opens on the fixed drives; a folder lists at once and its
  subfolders are measured in parallel, with a live activity log, the folder
  being read, elapsed time and file/folder/byte counters. Sizes are space on
  disk (sparse VM and WSL images count what they occupy). Tick rows and confirm
  to move them to the Recycle Bin.
- **Monitor:** health score, CPU with a one-minute history, memory, drives,
  network, battery and top processes, sampled every 2 s from plain Windows
  APIs while the page is open.
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

1. Clean: port the clean targets and default whitelist from `mo` 1.30.0 (MIT,
   tag `v1.30.0-windows`) into `src/MoleWin.Core`, scan then apply through
   `SafeRemover`. Keep the MIT notice in `third_party/`.
2. Apps (uninstall and leftovers), AI-tool cleanup, keep-awake.
3. Owner sign-off before reusing any Mole art; consumer name.
