# Changelog

All notable changes are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow [SemVer](https://semver.org/).

## [Unreleased]

### Security
- `SafeRemover` holds the folders above every path while it checks and
  recycles them, and checks each path again before its own attempt after a
  failed batch, so a folder swapped for a junction mid-removal cannot redirect
  it.
- `C:\Users\Public` and every other profile folder on the machine are
  protected themselves, like your own profile; their contents stay removable.
- Device-namespace paths with no drive after the prefix (`\\?\GLOBALROOT\...`,
  `\\?\Volume{...}\...`) are refused instead of passing the lexical check.
- Apps only reads install folders and icons from plain local drive paths, so
  a registry entry pointing at a network share is never opened.
- The uninstall confirmation names the program that will run, and MSI
  entries run `msiexec.exe` from the System32 folder by full path.
- Clean skips a cache folder that sits under a junction or symlink below its
  base folder (such as `%LOCALAPPDATA%`), and a rule aimed at a protected
  folder lists nothing.

### Fixed
- An unquoted uninstall command with `.exe` inside a folder name (such as
  `v2.exe.d`) no longer splits at that folder.
- A cache item that vanishes while Clean scans is skipped instead of failing
  the whole location.
- Age-limited Clean locations (Temp, Cursor workspaces) keep a folder when
  anything inside it was written recently, not only its direct children.
- An unknown language name in `settings.json` falls back to the system
  language instead of stopping the app at launch.
- A recycle Windows reports as done but that left an item on disk now
  reports that item as failed instead of recycled.

## [0.1.0] - 2026-10-16

### Added
- Monitor page: health line, CPU, memory and network with one-minute charts,
  drive capacity, top processes grouped by name, and Keep awake.
- Clean page: 76 cache locations in six groups (system, browsers, apps,
  graphics drivers, developer tools, AI tools), each marked safe to remove or
  worth a review.
- Apps page: installed apps with icons, sort by name, size or install date,
  search, uninstall through the app's own uninstaller, then unticked leftovers.
- Analyze page: drive and folder tree with used space against capacity and
  locked rows that say why they cannot be removed.
- Every removal goes to the Recycle Bin through `SafeRemover`, which resolves
  links and refuses protected paths and drives without a Recycle Bin.
- English and Arabic, light and dark themes, MSI installer.
