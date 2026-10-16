# Times Clean, Analyze and Apps actions against sandbox data (see tools/sandbox.ps1) by driving
# the Debug build through UI Automation: real buttons, the real confirmation dialog.
# Each timing runs from the confirm click to the moment the screen shows the result. "UI max"
# is the slowest UI Automation reply during the wait; the app answers on its UI thread, so a
# high value means the window was frozen.
# Run exactly: pwsh tools/time-actions.ps1 -Sandbox <folder>
param([Parameter(Mandatory)][string]$Sandbox)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$exe = Join-Path $PSScriptRoot "..\src\MoleWin.App\bin\Debug\net10.0-windows\MoleWin.exe"
$A = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

function Start-Page([string]$Page) {
    $env:MOLEWIN_SANDBOX = $Sandbox; $env:MOLEWIN_PAGE = $Page
    $p = Start-Process $exe -PassThru
    for ($i = 0; $i -lt 100 -and $p.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 100; $p.Refresh() }
    return @{ Process = $p; Root = $A::FromHandle($p.MainWindowHandle) }
}

# All descendants whose name matches; one UI Automation round trip.
function Find-Named($Root, [scriptblock]$Match) {
    $all = $Root.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    return @($all | Where-Object { & $Match $_.Current.Name })
}

# Polls until $Done returns true; reports seconds taken and the slowest single poll.
function Wait-Until([scriptblock]$Done, [int]$TimeoutSeconds = 120) {
    $clock = [Diagnostics.Stopwatch]::StartNew(); $worst = 0
    while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $poll = [Diagnostics.Stopwatch]::StartNew()
        $ok = & $Done
        $worst = [math]::Max($worst, $poll.ElapsedMilliseconds)
        if ($ok) { return @{ Seconds = [math]::Round($clock.Elapsed.TotalSeconds, 2); WorstMs = $worst } }
        Start-Sleep -Milliseconds 50
    }
    throw "timed out after $TimeoutSeconds s"
}

function Invoke-Element($Element) { $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }

# The first button whose name matches (its text label has the same name).
function Find-Button($Root, [scriptblock]$Match) {
    return (Find-Named $Root $Match | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button })[0]
}

# Answers Yes on the app's confirmation box. Its Yes and No are panes without an Invoke
# pattern (the Windows 11 message box), so the dialog gets the Yes command (IDYES) directly.
Add-Type -Namespace Win -Name Msg -MemberDefinition '[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);'
function Confirm-Dialog($Root) {
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        $dialogs = @($Root.FindAll($Scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)) |
            Where-Object { $_.FindFirst($Scope::Children, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty), "Yes")) })
        if ($dialogs.Count -gt 0) {
            [Win.Msg]::PostMessage([IntPtr]$dialogs[0].Current.NativeWindowHandle, 0x0111, [IntPtr]6, [IntPtr]::Zero) | Out-Null
            return
        }
        Start-Sleep -Milliseconds 50
    }
    throw "no confirmation dialog"
}

function Report([string]$Action, $Result) { "{0,-46} {1,6} s   UI max {2,5} ms" -f $Action, $Result.Seconds, $Result.WorstMs }

# Clean: scan on open, then recycle one whole group.
$app = Start-Page "Clean"
Report "Clean: scan on open" (Wait-Until { (Find-Named $app.Root { $args[0] -like "Scan finished*" }).Count -gt 0 })
Find-Named $app.Root { $args[0] -eq "System" } | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::CheckBox } |
    Select-Object -First 1 | ForEach-Object { $_.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
Invoke-Element (Find-Button $app.Root { $args[0] -like "Move * to the Recycle Bin" })
Confirm-Dialog $app.Root
Report "Clean: recycle System (2000 files)" (Wait-Until { (Find-Named $app.Root { $args[0] -eq "System" }).Count -eq 0 })
Stop-Process -Id $app.Process.Id

# Analyze: open the drive (lists and measures it), then recycle the widest folder.
$app = Start-Page "Analyze"
$drive = Wait-Until { $script:d = $app.Root.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::IsExpandCollapsePatternAvailableProperty), $true)); $null -ne $script:d }
$script:d.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
Report "Analyze: open drive, measure 4 folders" (Wait-Until { (Find-Named $app.Root { $args[0] -like "Finished*" }).Count -gt 0 })
(Find-Named $app.Root { $args[0] -eq "Games" } | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::CheckBox })[0].
    GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Invoke-Element (Find-Button $app.Root { $args[0] -eq "Move to Recycle Bin" })
Confirm-Dialog $app.Root
Report "Analyze: recycle Games (20000 files)" (Wait-Until { (Find-Named $app.Root { $args[0] -eq "Games" }).Count -eq 0 })
Stop-Process -Id $app.Process.Id

# Apps: uninstall (the fake uninstaller takes 3 s), then recycle what it left.
$app = Start-Page "Apps"
Wait-Until { (Find-Named $app.Root { $args[0] -eq "Sandbox Notes" }).Count -gt 0 } | Out-Null
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$item = (Find-Named $app.Root { $args[0] -eq "Sandbox Notes" })[0]
while ($item.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) { $item = $walker.GetParent($item) }
$item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Invoke-Element (Find-Button $app.Root { $args[0] -eq "Uninstall" })
Confirm-Dialog $app.Root
Report "Apps: uninstall to leftovers shown (3 s fake)" (Wait-Until { (Find-Named $app.Root { $args[0] -like "*appdata\Sandbox Notes" }).Count -gt 0 })
Find-Named $app.Root { $args[0] -like "*Sandbox Notes" } | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::CheckBox } |
    ForEach-Object { $_.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
Invoke-Element (Find-Button $app.Root { $args[0] -eq "Move to Recycle Bin" })
Confirm-Dialog $app.Root
Report "Apps: recycle leftovers" (Wait-Until { (Find-Named $app.Root { $args[0] -like "*appdata\Sandbox Notes" }).Count -eq 0 })
Stop-Process -Id $app.Process.Id

"recycled into the sandbox bin: $(@(Get-ChildItem (Join-Path $Sandbox 'RecycleBin')).Count) items"
