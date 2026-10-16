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
via REA and wants a native Windows equivalent. Research found Mole already
ships a reusable CLI engine, `mo`, written in Go, licensed GPL-3.0, with a
working Windows build on the `windows` branch. The paid Mac app is a separate
proprietary GUI over that same GPL CLI, so a GUI-wraps-CLI design has direct
precedent from the owner.

Decisions taken while planning: v1 wraps Mole's `mo` CLI (not Burrow's engine,
which is FSL-1.1 with a non-compete clause). Delivery is GitHub releases, not
commercial yet. Fan and battery control are dropped so the app needs no
elevated access and runs entirely as the normal user. Consumer name is
deferred to the owner conversation; internal codename stays `Mole-Win`.

Intended outcome: a WPF Fluent app that gives Windows users Mole's Clean,
Apps, AI-tool cleanup, Analyze, read-only Monitor, and keep-awake, reusing the
`mo` engine for everything it already does on Windows and adding native code
only where `mo` does not reach (AI-tool scanners).

## Scope

In: Clean, Apps (uninstall + leftovers), AI-tool cleanup, Analyze (disk
explorer), Monitor (read-only dashboard), keep-awake timer.

Out: fan-speed and battery-limit control (removed to avoid elevation), any
elevated service/broker, cross-platform shell with the Mac app, Microsoft
Store packaging, license-key activation, auto-update infrastructure. These
come only if the project needs them after the owner conversation.

## Architecture

Two processes, both running as the normal user, no elevation:

1. **GUI** - WPF + WPF-UI Fluent, built on the user's `wpf-fluent-skill`
   scaffold (.NET 10, CommunityToolkit.Mvvm, Serilog, SQLite/Dapper, resx
   localization, Core/Data/App projects, xUnit v3 tests, WiX v6 MSI). Scaffold
   with that skill's `new-project.bat`.

2. **Engine wrapper** - a `Core` service that runs the bundled `mo.exe` as a
   subprocess and parses its machine-readable output. One `{ok, data|error}`
   envelope per call; NDJSON for streamed commands; fall back to text parsing
   where a command has no JSON mode on Windows. This is the Windows analog of
   how Burrow drives the engine. Reuse the exact command surface: `clean`,
   `uninstall`, `optimize`, `analyze`, `status`, `purge`, `installer`.

`mo`'s own admin-gated steps (disk optimization, ARP flush, `clean --system`)
simply skip without admin; v1 runs them non-elevated and omits those steps
rather than shipping an elevated helper. If a user wants them later, `mo` can
request its own UAC prompt; the GUI stays unprivileged.

## Feature map

Reuse `mo` wherever it already works on Windows; build native modules only for
the gaps.

| Feature | Source | Notes |
|---------|--------|-------|
| Clean | `mo clean` | Preview with `--dry-run`, then apply. See safety note below. |
| Apps | `mo uninstall` | `--list` to enumerate from registry + Windows Apps, then uninstall + leftovers. |
| Analyze | `mo analyze --json` | Disk explorer; `mo` sends deletions to the Recycle Bin. |
| Optimize | `mo optimize` | DNS flush, disk health; admin-only steps are skipped (no elevation in v1). |
| Purge | `mo purge --paths` | Build artifacts in project folders. |
| Installer leftovers | `mo installer` | Find and remove installer files. |
| Monitor | `mo status` | Read-only: health score, CPU, memory, disk, power, network, processes. No fan/temp control. |
| AI-tool cleanup | native `Core` module | Mole's signature feature; build our own scanners for Claude Code / Codex / Cursor dirs and Ollama models if `mo clean` does not target them on Windows (verification item). Save a recovery copy before removal, matching Mole. |
| Keep-awake | native | `SetThreadExecutionState`; trivial, no dependency. |

Safety overlay: `mo clean`, `uninstall`, and `purge` delete permanently on
Windows (only `analyze` uses the Recycle Bin). The GUI must add a quarantine
or Recycle-Bin layer and show every item with a reason before calling apply,
matching the "nothing hard-deleted, shown first" behavior Mole's own UI gives.

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
   health score and sparklines for CPU/memory/disk from `mo status`, refreshed
   on a timer, rather than static readouts.
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
- **Local only, no exfiltration.** The app makes no network calls of its own
  and sends no user data anywhere. The only outbound traffic is `mo update`
  and `mo installer`, which the user triggers explicitly; AI-tool scanning
  reads local files and reports counts in-app, nothing leaves the machine.
- **Non-destructive by default.** Every destructive command previews first
  (`--dry-run`), and nothing is removed until the user confirms a list that
  shows each item with a reason. The GUI quarantine / Recycle-Bin layer makes
  removals reversible even where `mo` would delete permanently.
- **Protected paths.** Honor `mo`'s never-touch set (`C:\Windows`,
  `C:\Program Files`, Defender folders, profile root) and never add a path
  that widens it. AI-tool scanners are scoped to known per-tool directories,
  not free disk traversal.
- **Least privilege.** Runs entirely as the normal user, no elevated service,
  no driver, no kernel component. It reads no credentials, tokens, or secrets;
  AI-tool cleanup targets caches and chat history by path and does not parse or
  transmit their contents.
- **Supply chain.** Bundle the official signed `mo` release pinned by version
  and verified by checksum; never ship anything extracted from the decompiled
  DMG except art that the owner has cleared. Record `mo`'s version and hash in
  the repo.
- **Transparent and auditable.** All actions are logged locally (Serilog, plus
  `mo`'s own `operations.log`) so the user can see exactly what ran.

## Licensing

- `mo.exe` stays GPL-3.0; bundling it as an unmodified separate binary and
  invoking it at arm's length keeps the GUI a separate work. Ship `mo`'s
  source or a written offer alongside releases.
- GUI shell: MIT (same posture as Burrow's app shell) or GPL-3.0; decide at
  scaffold time. Non-commercial GitHub releases make this low-risk.
- Do not use `burrow-engine` (FSL-1.1 non-compete bars competing use).
- Reused Mole art (mascots, planets, logo) needs the owner's sign-off.
- Coordinate with the owner (tw93) before any commercial step.

## Project layout

From the `wpf-fluent-skill` scaffold (`new-project.bat`):

- `src/MoleWin.Core/` - engine wrapper, AI-tool scanners, models.
- `src/MoleWin.Data/` - SQLite history/quarantine, numbered migrations.
- `src/MoleWin.App/` - WPF Fluent GUI, view-models, pages (Clean, Apps, AI,
  Analyze, Monitor), mascot/animation assets.
- `tests/` - three xUnit v3 projects.
- `installer/` - WiX v6 MSI; bundles `mo.exe`. No service, no driver.
- `third_party/` - `mo.exe` and its license/source offer.

## Verification

End-to-end on a real Windows 10/11 machine, all as a normal user:

1. Build: `dotnet build -p:Platform=x64` (per the skill's scripts).
2. Engine wrap: confirm `mo status --json`, `mo analyze --json`, and streamed
   commands actually emit parseable JSON/NDJSON on the `windows` branch; run a
   `mo clean --dry-run` and show the preview in the GUI without deleting.
3. Apps: `mo uninstall --list` populates the Apps page; uninstall one app in a
   VM and confirm leftover cleanup.
4. AI cleanup: point the native scanner at seeded Claude/Codex/Cursor/Ollama
   dirs; confirm a recovery copy is written before removal.
5. Safety: confirm the quarantine/Recycle-Bin layer intercepts every
   non-dry-run `clean`/`uninstall`/`purge` and lists items with reasons first.
6. Monitor: `mo status` refreshes the gauge and sparklines on a timer with no
   elevation prompt.
7. Look and feel: mascot animates during a scan; Mica backdrop and page
   transitions render on Win11; light/dark switch works.
8. Tests: xUnit for the engine-output parser, AI scanner, and quarantine
   logic.

## Open items and risks

- **`mo` JSON coverage on Windows is unverified.** The README documents
  `--json` only for `history`. If `status`/`analyze` lack JSON on the
  `windows` branch, the wrapper must parse text, which is more fragile.
  Resolve first, at implementation start.
- **AI-tool dir coverage.** Confirm whether `mo clean` targets AI-tool dirs on
  Windows; if not, the native scanner is required (assumed required).
- **Permanent deletes.** The GUI quarantine layer must land before any
  non-dry-run `clean`/`uninstall`/`purge` is wired up.
- **Unsigned MSI on GitHub builds** will trip SmartScreen/Defender; acceptable
  for a non-commercial release but note it for users. Signing is a
  prerequisite for going commercial.
- **Owner sign-off** on reusing Mole's art and on the wrap-the-CLI posture
  before any release.
