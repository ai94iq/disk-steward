# Builds fake data for a Debug run with MOLEWIN_SANDBOX=<Root>. Recreates <Root> from scratch.
# Run exactly: pwsh tools/sandbox.ps1 -Root <folder>
# Nothing outside <Root> is read or written.
param([Parameter(Mandatory)][string]$Root)
$ErrorActionPreference = "Stop"

if (Test-Path $Root) { Remove-Item $Root -Recurse -Force }

# Files of the given sizes under one folder (zero-filled; allocated on disk, so sizes are real).
function Fill([string]$Folder, [int]$Count, [long]$Bytes) {
    New-Item -ItemType Directory -Force $Folder | Out-Null
    for ($i = 0; $i -lt $Count; $i++) {
        $stream = [IO.File]::Create((Join-Path $Folder "file$i.bin"))
        $stream.SetLength($Bytes)
        $stream.Dispose()
    }
}

# Clean: one place per category, a few thousand small files each.
foreach ($id in "temp", "chrome-cache", "spotify-data", "nvidia-dx", "npm-cache", "cursor-cache") {
    Fill (Join-Path $Root "clean\$id") 2000 16KB
}

# Analyze: a drive with one wide folder, a few large files, a deep tree and a small one.
$drive = Join-Path $Root "drive"
Fill (Join-Path $drive "Games\cache") 20000 4KB
Fill (Join-Path $drive "Videos") 5 50MB
for ($d = 0; $d -lt 50; $d++) { Fill (Join-Path $drive "Projects\repo$d\src") 100 8KB }
Fill (Join-Path $drive "Downloads") 300 64KB

# Apps: installs, and the data folders they leave behind when uninstalled.
foreach ($app in "Sandbox Notes", "Sandbox Player") {
    Fill (Join-Path $Root "apps\$app") 200 32KB
    Fill (Join-Path $Root "appdata\$app") 500 16KB
}
New-Item -ItemType Directory -Force (Join-Path $Root "shortcuts"), (Join-Path $Root "RecycleBin") | Out-Null

$files = (Get-ChildItem $Root -Recurse -File | Measure-Object Length -Sum)
"sandbox ready: $($files.Count) files, $([math]::Round($files.Sum / 1MB)) MB in $Root"
