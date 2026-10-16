# Builds fake data for a Debug run with DISKSTEWARD_SANDBOX=<Root>, shaped like a real Windows
# machine: about 330,000 files in 40,000 folders and 10 GB, close to the file and folder
# counts a real C:\ scan reads (the byte total is scaled down). Recreates <Root> from scratch.
# Run exactly: pwsh tools/sandbox.ps1 -Root <folder>
# Nothing outside <Root> is read or written.
param([Parameter(Mandatory)][string]$Root)
$ErrorActionPreference = "Stop"
$clock = [Diagnostics.Stopwatch]::StartNew()
if (Test-Path $Root) { Remove-Item $Root -Recurse -Force }

# $Count files of $Bytes each in one folder. SetLength allocates on disk without writing, so
# sizes are real (the scanner reads allocated size) and large files cost no time.
function Fill([string]$Folder, [int]$Count, [long]$Bytes, [string]$Ext = "bin") {
    [IO.Directory]::CreateDirectory($Folder) | Out-Null
    for ($i = 0; $i -lt $Count; $i++) {
        $stream = [IO.File]::Create([IO.Path]::Combine($Folder, "f$i.$Ext"))
        if ($Bytes -gt 0) { $stream.SetLength($Bytes) }
        $stream.Dispose()
    }
}

# A tree $Depth levels deep with $Fan folders per level and $Files files in each leaf, like
# node_modules or WinSxS.
function Tree([string]$Folder, [int]$Depth, [int]$Fan, [int]$Files, [long]$Bytes) {
    if ($Depth -eq 0) { Fill $Folder $Files $Bytes; return }
    for ($i = 0; $i -lt $Fan; $i++) { Tree ([IO.Path]::Combine($Folder, "d$i")) ($Depth - 1) $Fan $Files $Bytes }
}

# Clean: one folder per rule id (the ones a typical machine has), with the file counts
# those caches really hold. The app maps each folder to its rule.
$clean = @{
    "temp" = 6000, 64KB; "wer-archive" = 200, 256KB; "wer-queue" = 60, 256KB; "crash-dumps" = 12, 64MB
    "chrome-cache" = 25000, 32KB; "chrome-code" = 3000, 64KB; "chrome-gpu" = 40, 1MB; "chrome-sw" = 8000, 16KB
    "chrome-shader" = 60, 2MB; "edge-cache" = 15000, 32KB; "edge-code" = 2000, 64KB; "edge-gpu" = 40, 1MB
    "edge-sw" = 6000, 16KB; "edge-shader" = 50, 2MB; "firefox-cache" = 10000, 32KB
    "discord-cache" = 4000, 32KB; "discord-code" = 800, 64KB; "slack-cache" = 3000, 32KB; "teams-cache" = 2500, 32KB
    "vscode-cache" = 1500, 32KB; "vscode-cached-data" = 600, 256KB; "spotify-data" = 4000, 128KB
    "epic-webcache" = 1200, 32KB; "steam-htmlcache" = 900, 64KB
    "nvidia-dx" = 300, 8MB; "nvidia-gl" = 150, 4MB; "d3d-cache" = 200, 2MB; "amd-vk" = 100, 4MB
    "npm-cache" = 40000, 8KB; "yarn-cache" = 15000, 16KB; "pip-cache" = 3000, 64KB; "nuget-http" = 2000, 256KB
    "go-build" = 6000, 16KB; "bun-cache" = 8000, 16KB
    "cursor-cache" = 2000, 32KB; "claude-chats" = 150, 512KB
}
foreach ($id in $clean.Keys) { Fill ([IO.Path]::Combine($Root, "clean", $id)) $clean[$id][0] $clean[$id][1] }

# Analyze: a drive laid out like C:\.
$d = [IO.Path]::Combine($Root, "drive")
Tree ([IO.Path]::Combine($d, "Windows", "WinSxS")) 2 120 4 16KB                 # 57,600 files
Fill ([IO.Path]::Combine($d, "Windows", "System32")) 6000 128KB
for ($a = 0; $a -lt 120; $a++) { Fill ([IO.Path]::Combine($d, "Program Files", "App $a")) 250 64KB }
for ($a = 0; $a -lt 60; $a++) { Fill ([IO.Path]::Combine($d, "Program Files (x86)", "App $a")) 200 64KB }
Tree ([IO.Path]::Combine($d, "ProgramData", "Packages")) 2 40 12 32KB            # 19,200 files
$user = [IO.Path]::Combine($d, "Users", "me")
Tree ([IO.Path]::Combine($user, "AppData", "Local", "Packages")) 3 20 4 16KB      # 32,000 files
for ($r = 0; $r -lt 30; $r++) { Tree ([IO.Path]::Combine($user, "source", "repo$r", "node_modules")) 3 10 2 4KB } # 60,000 files
Fill ([IO.Path]::Combine($user, "Videos")) 12 300MB "mp4"
Fill ([IO.Path]::Combine($user, "Downloads")) 400 4MB
Fill ([IO.Path]::Combine($user, "Documents")) 3000 256KB "docx"
Fill ([IO.Path]::Combine($user, "Pictures")) 4000 2MB "jpg"
Fill ([IO.Path]::Combine($d, "Games", "Big Game")) 6 400MB "pak"
Fill ([IO.Path]::Combine($d, "Games", "Big Game", "shaders")) 20000 4KB

# Apps: 170 installed apps; the uninstall leaves each one's data folder behind.
for ($a = 0; $a -lt 170; $a++) {
    $name = "Sandbox App {0:D3}" -f $a
    Fill ([IO.Path]::Combine($Root, "apps", $name)) 20 256KB
    Fill ([IO.Path]::Combine($Root, "appdata", $name)) 30 64KB
}
[IO.Directory]::CreateDirectory([IO.Path]::Combine($Root, "shortcuts")) | Out-Null
[IO.Directory]::CreateDirectory([IO.Path]::Combine($Root, "RecycleBin")) | Out-Null

$files = [IO.Directory]::EnumerateFiles($Root, "*", [IO.SearchOption]::AllDirectories) | ForEach-Object { [IO.FileInfo]::new($_).Length } | Measure-Object -Sum
$folders = @([IO.Directory]::EnumerateDirectories($Root, "*", [IO.SearchOption]::AllDirectories)).Count
"sandbox ready: $($files.Count) files, $folders folders, $([math]::Round($files.Sum / 1GB, 1)) GB in $Root ($([int]$clock.Elapsed.TotalSeconds) s)"
