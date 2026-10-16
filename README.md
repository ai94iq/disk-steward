# Mole-Win

Windows system-maintenance app (internal codename `Mole-Win`; consumer name
TBD). A native WPF Fluent app with its own C# engine that gives Windows users
cleanup, app uninstall, AI-tool cleanup, disk analysis, and a read-only system
monitor.

Status: planning only. No app code yet; this repo currently holds the design
and research. Act on `docs/plan.md` to start building.

## Contents

- [What this is](#what-this-is)
- [Docs](#docs)
- [Next steps](#next-steps)

## What this is

A Windows counterpart to tw93's macOS app "Mole". The engine is native C#;
cleanup rules are ported from the MIT-licensed `mo` 1.30.0 Windows build (the
Windows `mo` has no JSON or non-interactive mode, so wrapping it was dropped).
Runs entirely as the normal user, no elevation. See `docs/plan.md` for the
full design and `docs/rea-findings.md` for the analysis of the original app
that informed it.

## Docs

- `docs/plan.md` - architecture, scope, feature map, look and feel, safeguards,
  licensing, verification, and open items.
- `docs/rea-findings.md` - static analysis of the original macOS Mole app.

## Next steps

1. Generate the .NET solution with the `wpf-fluent-skill` `new-project.bat`
   (needs .NET 10; not run yet).
2. Port the protected paths, whitelist and clean targets from `mo` 1.30.0 into
   `src/MoleWin.Core`, with the quarantine/Recycle-Bin layer first.
