param(
    [ValidateSet('Install','Uninstall')][string]$Mode = 'Install',
    [string]$Payload,
    [ValidateSet('standard','full')][string]$Variant = 'standard',
    [string]$SetupPath,
    [string]$TestRoot,
    [ValidateSet('','AfterBackup','AfterPromotion','AfterHealth','AfterRegistration')][string]$FailurePoint = '',
    [switch]$RemoveSettings
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$product = 'VolturaWeekNumber'
$registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\VolturaWeekNumber'
$base = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs'
if ($TestRoot) {
    $base = [IO.Path]::GetFullPath($TestRoot)
    if (-not (Test-Path -LiteralPath (Join-Path $base '.voltura-installer-test') -PathType Leaf)) { throw 'Test root must have an explicit test marker.' }
}
$target = [IO.Path]::GetFullPath((Join-Path $base $product))
$control = Join-Path $base '.VolturaWeekNumber-maintenance'
$journalPath = Join-Path $control 'journal.json'
$markerText = 'VolturaWeekNumber.Installation.v1'
$script:transaction = $null
$script:completedRemoval = $false
$script:restartPrevious = $false

function Assert-OwnedPath([string]$path, [switch]$AllowTarget) {
    $resolved = [IO.Path]::GetFullPath($path)
    if ($AllowTarget -and $resolved -eq $target) { return }
    if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath($base)) { throw 'Maintenance path escaped installation parent.' }
    if ([IO.Path]::GetFileName($resolved) -notmatch '^\.VolturaWeekNumber-(stage|backup|removed)-[a-f0-9]{32}$') { throw 'Unrecognized maintenance directory.' }
}
function Assert-NoLinks([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $node = Get-Item -LiteralPath $path -Force
    if ($node.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked installation directories are not supported.' }
    foreach ($child in Get-ChildItem -LiteralPath $path -Force -Recurse) {
        if ($child.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked installation files are not supported.' }
    }
}
function Assert-Installation([string]$path) {
    Assert-NoLinks $path
    $marker = Join-Path $path 'installation.marker'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or [IO.File]::ReadAllText($marker).Trim() -ne $markerText) { throw 'The existing directory is not an owned Voltura WeekNumber installation.' }
}
function Remove-Owned([string]$path) {
    Assert-OwnedPath $path
    Assert-NoLinks $path
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
function Write-Journal {
    $temporary = $journalPath + '.pending'
    [IO.File]::WriteAllText($temporary, ($script:transaction | ConvertTo-Json -Depth 8))
    if (Test-Path -LiteralPath $journalPath) { [IO.File]::Replace($temporary, $journalPath, [NullString]::Value) }
    else { [IO.File]::Move($temporary, $journalPath) }
}
function Stop-OwnedProcess {
    $expected = Join-Path $target 'VolturaWeekNumber.exe'
    foreach ($process in Get-Process -Name VolturaWeekNumber -ErrorAction SilentlyContinue) {
        if ($process.Path -and [IO.Path]::GetFullPath($process.Path) -eq $expected) {
            if ($Mode -eq 'Install') { $script:restartPrevious = $true }
            Stop-Process -Id $process.Id -Force
            if (-not $process.WaitForExit(15000)) { throw 'The installed application did not stop.' }
        }
    }
}
function Register-Installation([string]$installVariant) {
    if ($TestRoot) { return }
    $version = (Get-Item -LiteralPath (Join-Path $target 'VolturaWeekNumber.exe')).VersionInfo.ProductVersion.Split('+')[0]
    New-Item -Path $registryPath -Force | Out-Null
    $values = @{
        DisplayName = 'Voltura WeekNumber'; DisplayVersion = $version; Publisher = 'Voltura AB'; InstallLocation = $target
        UninstallString = ('"' + (Join-Path $target 'Uninstall.exe') + '"')
        ModifyPath = ('"' + (Join-Path $target 'Maintenance.exe') + '"')
        DisplayIcon = (Join-Path $target 'VolturaWeekNumber.exe'); PackageVariant = $installVariant
        URLInfoAbout = 'https://github.com/voltura/voltura-weeknumber'
    }
    foreach ($name in $values.Keys) { New-ItemProperty -Path $registryPath -Name $name -Value $values[$name] -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $registryPath -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    $startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Voltura WeekNumber.lnk'
    $shell = New-Object -ComObject WScript.Shell
    try { $shortcut = $shell.CreateShortcut($startMenu); $shortcut.TargetPath = Join-Path $target 'VolturaWeekNumber.exe'; $shortcut.WorkingDirectory = $target; $shortcut.Save() }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}
function Restore-Transaction {
    if (-not $script:transaction) { return }
    $tx = $script:transaction
    Assert-OwnedPath $tx.Stage
    Assert-OwnedPath $tx.Backup
    if ($tx.Mode -eq 'Uninstall' -and $tx.Phase -eq 'Removing') {
        Complete-Removal $tx.Backup
        Remove-Item -LiteralPath $journalPath -Force
        $script:completedRemoval = $true
        return
    }
    if (Test-Path -LiteralPath $tx.Backup) {
        Assert-Installation $tx.Backup
        Stop-OwnedProcess
        if (Test-Path -LiteralPath $target) {
            Assert-Installation $target
            $failed = Join-Path $base ('.VolturaWeekNumber-stage-' + [Guid]::NewGuid().ToString('N'))
            Move-Item -LiteralPath $target -Destination $failed
            Remove-Owned $failed
        }
        Move-Item -LiteralPath $tx.Backup -Destination $target
        Register-Installation $tx.PreviousVariant
    } elseif (-not $tx.HadPrevious -and $tx.Phase -in @('Promoting','Promoted','Healthy','Registered')) {
        if (Test-Path -LiteralPath $target) {
            Assert-Installation $target
            Stop-OwnedProcess
            Move-Item -LiteralPath $target -Destination $tx.Stage
            Remove-Owned $tx.Stage
        }
        if (-not $TestRoot -and (Test-Path $registryPath)) { Remove-Item -LiteralPath $registryPath -Recurse -Force }
    }
    if (Test-Path -LiteralPath $tx.Stage) { Remove-Owned $tx.Stage }
    Remove-Item -LiteralPath $journalPath -Force -ErrorAction SilentlyContinue
}
function Complete-Removal([string]$removalPath) {
    # Removal is forward-recoverable once deletion starts; never restore a partial directory.
    Remove-Owned $removalPath
    if (-not $TestRoot) {
        if (Test-Path $registryPath) { Remove-Item -LiteralPath $registryPath -Recurse -Force }
        $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Voltura WeekNumber.lnk'
        Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue
        $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        $expectedCommand = '"' + (Join-Path $target 'VolturaWeekNumber.exe') + '" --autostart'
        $entry = Get-ItemPropertyValue -Path $run -Name 'Voltura WeekNumber' -ErrorAction SilentlyContinue
        if ($entry -eq $expectedCommand) { Remove-ItemProperty -Path $run -Name 'Voltura WeekNumber' }
    }
}
function Fail-At([string]$point) { if ($FailurePoint -eq $point) { throw ('Controlled maintenance failure: ' + $point) } }

try {
    New-Item -ItemType Directory -Path $base -Force | Out-Null
    Assert-NoLinks $control
    New-Item -ItemType Directory -Path $control -Force | Out-Null
    $lock = [IO.File]::Open((Join-Path $control 'maintenance.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    if (Test-Path -LiteralPath $journalPath) {
        if ((Get-Item -LiteralPath $journalPath).Length -gt 65536) { throw 'Invalid maintenance journal.' }
        $script:transaction = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
        Restore-Transaction
        if ($script:completedRemoval -and $Mode -eq 'Uninstall') { exit 0 }
        $script:transaction = $null
    }
    $hadPrevious = Test-Path -LiteralPath $target
    if ($hadPrevious) { Assert-Installation $target }
    $stage = Join-Path $base ('.VolturaWeekNumber-stage-' + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $base ('.VolturaWeekNumber-backup-' + [Guid]::NewGuid().ToString('N'))
    $previousVariant = 'standard'
    if ($hadPrevious -and (Test-Path -LiteralPath (Join-Path $target 'package.variant'))) { $previousVariant = [IO.File]::ReadAllText((Join-Path $target 'package.variant')).Trim() }
    if ($previousVariant -notin @('standard','full')) { throw 'Invalid installed package variant.' }
    $script:transaction = [ordered]@{ Mode = $Mode; Stage = $stage; Backup = $backup; HadPrevious = [bool]$hadPrevious; PreviousVariant = $previousVariant; Phase = 'Preparing' }
    Write-Journal
    if ($Mode -eq 'Uninstall') {
        if (-not $hadPrevious) { throw 'No owned installation found.' }
        Stop-OwnedProcess
        if (-not $TestRoot) {
            $recovery = Join-Path $control 'RecoveryUninstall.exe'
            Copy-Item -LiteralPath (Join-Path $target 'Uninstall.exe') -Destination $recovery -Force
            New-ItemProperty -Path $registryPath -Name UninstallString -Value ('"' + $recovery + '"') -PropertyType String -Force | Out-Null
        }
        Move-Item -LiteralPath $target -Destination $backup
        $script:transaction.Phase = 'Removing'; Write-Journal
        if (-not $TestRoot) {
            if ($RemoveSettings) {
                $data = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Voltura\WeekNumber'))
                Assert-NoLinks $data
                if (Test-Path -LiteralPath $data) { Remove-Item -LiteralPath $data -Recurse -Force }
            }
        }
        Complete-Removal $backup
    } else {
        if (-not $Payload -or -not $SetupPath) { throw 'Payload and original installer are required.' }
        Assert-NoLinks $Payload
        $manifestFile = Join-Path $Payload 'payload.json'
        if ((Get-Item -LiteralPath $manifestFile).Length -gt 1048576) { throw 'Payload manifest too large.' }
        $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
        New-Item -ItemType Directory -Path $stage | Out-Null
        $seen = @{}
        foreach ($entry in $manifest) {
            $relative = [string]$entry.path
            if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)' -or $relative.Contains(':') -or $seen.ContainsKey($relative)) { throw 'Invalid or duplicate payload path.' }
            $seen[$relative] = $true
            $source = [IO.Path]::GetFullPath((Join-Path $Payload $relative))
            if (-not $source.StartsWith([IO.Path]::GetFullPath($Payload).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Payload path escaped root.' }
            if ((Get-Item -LiteralPath $source).Length -ne $entry.size -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) { throw ('Payload integrity check failed: ' + $relative) }
            $destination = Join-Path $stage $relative
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.sha256) { throw 'Staged payload integrity check failed.' }
        }
        foreach ($required in @('VolturaWeekNumber.exe','VolturaWeekNumber.dll','VolturaWeekNumber.runtimeconfig.json','VolturaWeekNumber.deps.json')) {
            if (-not $seen.ContainsKey($required)) { throw ('Required payload file missing: ' + $required) }
        }
        Copy-Item -LiteralPath $SetupPath -Destination (Join-Path $stage 'Maintenance.exe')
        [IO.File]::WriteAllText((Join-Path $stage 'installation.marker'), $markerText)
        [IO.File]::WriteAllText((Join-Path $stage 'package.variant'), $Variant)
        $script:transaction.Phase = 'Staged'; Write-Journal
        Stop-OwnedProcess
        if ($hadPrevious) { Move-Item -LiteralPath $target -Destination $backup }
        Fail-At 'AfterBackup'
        $script:transaction.Phase = 'Promoting'; Write-Journal
        Move-Item -LiteralPath $stage -Destination $target
        $script:transaction.Phase = 'Promoted'; Write-Journal
        Fail-At 'AfterPromotion'
        $health = Start-Process -FilePath (Join-Path $target 'VolturaWeekNumber.exe') -ArgumentList '--installer-health-check' -PassThru -WindowStyle Hidden
        if (-not $health.WaitForExit(30000)) { $health.Kill(); throw 'Application health check timed out.' }
        if ($health.ExitCode -ne 0) { throw 'Application health check failed.' }
        $health.Dispose()
        $script:transaction.Phase = 'Healthy'; Write-Journal
        Fail-At 'AfterHealth'
        Register-Installation $Variant
        $script:transaction.Phase = 'Registered'; Write-Journal
        Fail-At 'AfterRegistration'
        if ($hadPrevious) { Remove-Owned $backup }
    }
    Remove-Item -LiteralPath $journalPath -Force
    exit 0
} catch {
    $failure = $_.Exception.Message
    try { Restore-Transaction } catch { $failure += ' Recovery requires retrying setup: ' + $_.Exception.Message }
    if ($script:restartPrevious -and (Test-Path -LiteralPath (Join-Path $target 'VolturaWeekNumber.exe'))) {
        try { Start-Process -FilePath (Join-Path $target 'VolturaWeekNumber.exe') -ArgumentList '--autostart' -WindowStyle Hidden | Out-Null }
        catch { $failure += ' Please reopen Voltura WeekNumber manually.' }
    }
    [Console]::Error.WriteLine($failure)
    exit 1
} finally {
    if (Get-Variable lock -ErrorAction SilentlyContinue) { if ($lock) { $lock.Dispose() } }
}
