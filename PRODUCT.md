# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

<!-- Actual platform is a Windows 10/11 desktop app built with WPF (.NET 10, net10.0-windows)
and the WPF-UI (Fluent) library. Recorded as `web` only because the schema has no desktop value;
Impeccable's web detector and live-browser tooling do not apply. UI work here is a hand audit of
XAML, styles and resources, verified with screenshots of the running app. -->

## Users

One person on their own Windows PC: the owner-operator of the machine, not an IT admin managing
others. They want to understand what is eating disk space and free some, without fear of breaking
Windows or losing their own files. English and Arabic speakers; the app ships both, right-to-left
included.

## Product Purpose

Disk Steward measures disk use, offers safe cache cleanup, lists installed apps with their
leftovers, and shows a live system monitor. It exists so a non-expert can reclaim space and watch
machine health with confidence that nothing irreversible happens: every removal goes to the Recycle
Bin, never a permanent delete, and the app never runs elevated.

## Positioning

Safety is the mechanism, not a disclaimer. One guard (`SafeRemover`) re-checks every path at the
moment of removal against protected system and personal folders and resolves links to their real
target, so a cache path cannot be redirected into something that matters. A cleaner that competes on
"frees the most" could not truthfully copy the stance of refusing anything it cannot prove is safe.

## Operating Context

A desktop window with a left navigation rail and four pages: Monitor, Clean, Apps, Analyze. Launched
and used ad hoc, as a standard (non-admin) user, on the person's own machine. Actions that remove
anything require a tick plus a confirmation, and land in the Recycle Bin. A Debug-only sandbox lets
the developer exercise every page against fake data.

## Capabilities and Constraints

- **Monitor:** health line, CPU, memory and network with one-minute charts, drive capacity, top
  processes grouped by name, and Keep awake.
- **Clean:** cache locations in groups (system, browsers, apps, graphics drivers, developer tools,
  AI tools), each scanned read-only; nothing is pre-ticked.
- **Apps:** installed apps with icons, sort and search, uninstall through the app's own uninstaller,
  then unticked leftovers.
- **Analyze:** drive and folder tree with used space against capacity, and locked rows that say why
  a folder cannot be removed.
- Constraints: Windows 10/11 only, WPF + WPF-UI, CommunityToolkit.Mvvm, runs `asInvoker` (never
  elevated), SQLite for its own small store. Localized English (satellite) and Arabic (neutral,
  RTL), light and dark themes with an accent choice.

## Brand Commitments

- **Name:** Disk Steward.
- **Voice:** plain, reassuring, non-technical where it can be; a locked row explains itself in the
  user's terms rather than showing an error.
- **Identity:** Fluent/WPF-UI foundation, dark and light, with an accent color. Bilingual with full
  RTL. These are binding and preserved; this pass pushes craft within that identity, not away from
  it.

## Evidence on Hand

Real, shipping code and four working pages (see `src/DiskSteward.App/Features`). README screenshots
in `docs/images` (analyze, apps, clean, monitor). No testimonials, customers, benchmarks, pricing or
deployment claims exist; future work must not invent them.

## Product Principles

1. Reversible by default: removals go to the Recycle Bin, never a permanent delete.
2. Prove safe before touching: re-check at the moment of action, resolve links, refuse the unknown.
3. The user is never the admin of anyone but themselves; never require elevation.
4. Say why, in the user's words: a refusal or a locked row explains itself plainly.
5. Bilingual and bidirectional as a first-class fact, not an afterthought.

## Accessibility & Inclusion

Full right-to-left support for Arabic is a shipped requirement, including layout mirroring and
Arabic-Indic digits as an option. Text must stay legible in both light and dark themes.
