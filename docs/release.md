# Release checklist

1. Move CHANGELOG `Unreleased` entries under the new version heading with today's date.
2. Bump `Version` in `Directory.Build.props` (MAJOR.MINOR.PATCH, always higher than the last shipped MSI).
3. Run `package.bat`.
4. Sign `DiskSteward.exe`, then rebuild and sign the MSI.
5. Test on a Windows 10 22H2 VM and on Windows 11, as a standard user: fresh install, upgrade from the previous MSI, launch, uninstall (user data must remain), downgrade blocked.
6. Commit: `repo: build: release vX.Y.Z` with the changelog summary in the body.
7. Tag: `git tag -a vX.Y.Z -m "vX.Y.Z"`.
8. Push the tag: CI (`.github/workflows/ci.yml`) runs the tests, builds the MSI and attaches
   it to a GitHub release with a `.msi.sha256` file next to it. That MSI is unsigned; replace
   it with the signed one from step 4, and replace the `.sha256` file with the signed MSI's
   hash (`Get-FileHash <msi> -Algorithm SHA256`).

CI actions are pinned to full commit SHAs, with the release in a comment. To update one, look
up the commit of its latest release (`gh api repos/<owner>/<action>/releases/latest`) and
replace both. Repository settings that the workflow file cannot set: a tag ruleset that limits
who may create `v*` tags, and branch protection on `main`.
