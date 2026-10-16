# Mole-Win

Windows system-maintenance app (internal codename `Mole-Win`; consumer name
TBD). A native WPF Fluent GUI that wraps Mole's `mo` CLI engine to give Windows
users cleanup, app uninstall, AI-tool cleanup, disk analysis, and a read-only
system monitor.

Status: planning only. No app code yet; this repo currently holds the design
and research. Act on `docs/plan.md` to start building.

## Contents

- [What this is](#what-this-is)
- [Docs](#docs)
- [Next steps](#next-steps)

## What this is

A Windows counterpart to tw93's macOS app "Mole", built by wrapping Mole's own
open-source `mo` CLI (Go, GPL-3.0) rather than reimplementing it. Runs entirely
as the normal user, no elevation. See `docs/plan.md` for the full design and
`docs/rea-findings.md` for the analysis of the original app that informed it.

## Docs

- `docs/plan.md` - architecture, scope, feature map, look and feel, safeguards,
  licensing, verification, and open items.
- `docs/rea-findings.md` - static analysis of the original macOS Mole app.

## Next steps

1. Resolve the open items in `docs/plan.md` (mainly: does the Windows `mo`
   build emit JSON for `status`/`analyze`).
2. Generate the .NET solution with the `wpf-fluent-skill` `new-project.bat`
   (needs .NET 10; not run yet).
3. Build the engine wrapper in `src/MoleWin.Core`.
