param([string]$ShellPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$test = Join-Path $root ('artifacts\installer-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $test -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $test '.voltura-installer-test'), 'Isolated maintenance test')
$payload = Join-Path $root 'artifacts\payload-standard'
$version = (Get-Content (Join-Path $root 'version.json') -Raw | ConvertFrom-Json).version
$setup = Join-Path $root "artifacts\publish\VolturaWeekNumber-Setup-$version-win-x64.exe"
$helper = Join-Path $root 'installer\maintain.ps1'
$target = Join-Path $test 'VolturaWeekNumber'

$shell = if ($ShellPath)
{
    $ShellPath
}
else
{
    (Get-Process -Id $PID).Path
}

$variant = 'standard'
$checks = [Collections.Generic.List[string]]::new()
function Prepare-TestPayload([string]$source, [string]$name)
{
    $prepared = Join-Path $test $name
    Copy-Item -LiteralPath $source -Destination $prepared -Recurse
    $originalEntries = Get-Content -LiteralPath (Join-Path $prepared 'payload.json') -Raw | ConvertFrom-Json
    # The maintenance helper copies/verifies this fixture; it never executes it.
    $uninstallerPath = Join-Path $prepared 'Uninstall.exe'
    [IO.File]::WriteAllBytes($uninstallerPath, [byte[]](77, 90, 1, 2, 3, 4))
    & $shell `
        -NoProfile `
        -NonInteractive `
        -ExecutionPolicy Bypass `
        -File (Join-Path $root 'installer\prepare-payload.ps1') `
        -Payload $prepared

    if ($LASTEXITCODE)
    {
        throw 'NSIS payload preparation failed.'
    }

    $preparedEntries = Get-Content -LiteralPath (Join-Path $prepared 'payload.json') -Raw | ConvertFrom-Json

    if (@($preparedEntries).Count -ne (@($originalEntries).Count + 1))
    {
        throw 'Prepared manifest entry count changed.'
    }

    foreach ($entry in $preparedEntries)
    {
        if (-not $entry.path -or
            -not $entry.sha256 -or
            $null -eq $entry.size)
        {
            throw 'Prepared manifest contains a wrapped or incomplete entry.'
        }
    }

    $uninstallerEntry = @($preparedEntries | Where-Object path -eq 'Uninstall.exe')

    if ($uninstallerEntry.Count -ne 1 -or
        $uninstallerEntry[0].sha256 -ne (Get-FileHash -LiteralPath $uninstallerPath).Hash)
    {
        throw 'Generated uninstaller hash missing.'
    }

    $checks.Add("NSIS manifest preparation: $name")

    return $prepared
}
$payload = Prepare-TestPayload $payload 'prepared-standard'
function Run-Maintenance([string]$mode, [string]$failure = '')
{
    $arguments = @(
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $helper,
        '-Mode',
        $mode,
        '-Payload',
        $payload,
        '-SetupPath',
        $setup,
        '-TestRoot',
        $test,
        '-Variant',
        $variant)

    if ($failure)
    {
        $arguments += @('-FailurePoint', $failure)
    }

    & $shell @arguments

    return $LASTEXITCODE
}

if ((Run-Maintenance 'Install' 'AfterPromotion') -eq 0 -or
    (Test-Path -LiteralPath $target))
{
    throw 'Fresh installation rollback failed.'
}

$checks.Add('Fresh installation rollback')

if ((Run-Maintenance 'Install') -ne 0)
{
    throw 'Fresh isolated installation failed.'
}

$checks.Add('Fresh installation and health check')
$original = (Get-FileHash -LiteralPath (Join-Path $target 'VolturaWeekNumber.exe')).Hash

foreach ($point in @('AfterBackup', 'AfterPromotion', 'AfterHealth', 'AfterRegistration'))
{
    if ((Run-Maintenance 'Install' $point) -eq 0)
    {
        throw "Expected failure at $point."
    }

    if ((Get-FileHash -LiteralPath (Join-Path $target 'VolturaWeekNumber.exe')).Hash -ne $original)
    {
        throw "Rollback failed at $point."
    }

    if (Test-Path -LiteralPath (Join-Path $test '.VolturaWeekNumber-maintenance\journal.json'))
    {
        throw "Journal not closed at $point."
    }

    $checks.Add("Rollback after $point")
}

if ((Run-Maintenance 'Install') -ne 0)
{
    throw 'Upgrade failed.'
}

$checks.Add('Upgrade and health check')
$cleanPayload = $payload
$payload = Join-Path $test 'corrupt-payload'
Copy-Item -LiteralPath $cleanPayload -Destination $payload -Recurse
[IO.File]::AppendAllText((Join-Path $payload 'VolturaWeekNumber.dll'), 'corrupted')

if ((Run-Maintenance 'Install') -eq 0)
{
    throw 'Corrupt payload must be rejected.'
}

if ((Get-FileHash -LiteralPath (Join-Path $target 'VolturaWeekNumber.exe')).Hash -ne $original)
{
    throw 'Corrupt payload changed installed application.'
}

$checks.Add('Reject corrupt payload before stopping or replacing installation')
$payload = $cleanPayload
$backup = Join-Path $test ('.VolturaWeekNumber-backup-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $test ('.VolturaWeekNumber-stage-' + [Guid]::NewGuid().ToString('N'))
Move-Item -LiteralPath $target -Destination $backup
$journal = [ordered]@{
    Mode = 'Install'
    Stage = $stage
    Backup = $backup
    HadPrevious = $true
    PreviousVariant = 'standard'
    Phase = 'Staged'
}
[IO.File]::WriteAllText(
    (Join-Path $test '.VolturaWeekNumber-maintenance\journal.json'),
    ($journal | ConvertTo-Json))

if ((Run-Maintenance 'Install') -ne 0)
{
    throw 'Interrupted upgrade recovery failed.'
}

$checks.Add('Recover an interrupted upgrade from its journal')

if ((Run-Maintenance 'Uninstall') -ne 0 -or (Test-Path -LiteralPath $target))
{
    throw 'Uninstall failed.'
}

$checks.Add('Owned uninstall')

if ((Run-Maintenance 'Install') -ne 0)
{
    throw 'Setup for interrupted removal failed.'
}

$backup = Join-Path $test ('.VolturaWeekNumber-backup-' + [Guid]::NewGuid().ToString('N'))
Move-Item -LiteralPath $target -Destination $backup
Remove-Item -LiteralPath (Join-Path $backup 'installation.marker')
$journal = [ordered]@{
    Mode = 'Uninstall'
    Stage = $stage
    Backup = $backup
    HadPrevious = $true
    PreviousVariant = 'standard'
    Phase = 'Removing'
}
[IO.File]::WriteAllText(
    (Join-Path $test '.VolturaWeekNumber-maintenance\journal.json'),
    ($journal | ConvertTo-Json))

if ((Run-Maintenance 'Uninstall') -ne 0 -or
    (Test-Path -LiteralPath $backup))
{
    throw 'Interrupted partial removal did not complete.'
}

$checks.Add('Finish interrupted partial removal without restoring incomplete files')
$payload = Prepare-TestPayload (Join-Path $root 'artifacts\payload-full') 'prepared-full'
$setup = Join-Path $root "artifacts\publish\VolturaWeekNumber-Setup-$version-win-x64-full.exe"
$variant = 'full'

if ((Run-Maintenance 'Install') -ne 0)
{
    throw 'Full offline payload installation failed.'
}

if ([IO.File]::ReadAllText((Join-Path $target 'package.variant')).Trim() -ne 'full')
{
    throw 'Full installer variant was not retained.'
}

$checks.Add('Full offline payload installation and health check')

if ((Run-Maintenance 'Uninstall') -ne 0)
{
    throw 'Full offline payload uninstall failed.'
}

$checks.Add('Full offline payload uninstall')
New-Item -ItemType Directory -Path $target | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'unrelated.txt'), 'preserve')

if ((Run-Maintenance 'Install') -eq 0)
{
    throw 'Unowned target must be rejected.'
}

if ([IO.File]::ReadAllText((Join-Path $target 'unrelated.txt')) -ne 'preserve')
{
    throw 'Unrelated file changed.'
}

$checks.Add('Reject unowned existing directory')
$report = [ordered]@{
    passed = $checks.Count
    checks = @($checks)
    shell = $shell
    root = $test
    utc = [DateTimeOffset]::UtcNow.ToString('O')
}
[IO.File]::WriteAllText(
    (Join-Path $root 'artifacts\installer-test-results.json'),
    ($report | ConvertTo-Json -Depth 4))
$checks | Write-Output
