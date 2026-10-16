# Changelog

All notable changes are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow [SemVer](https://semver.org/).

## [Unreleased]

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
