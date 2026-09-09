#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$KeyPath = $env:WEEKNUMBER_KEYPATH,
    [string]$Version,
    [switch]$PrepareOnly,
    [switch]$NoTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repository = 'voltura/voltura-weeknumber'

function ConvertTo-ReleaseVersion([string]$Value)
{
    if ($Value -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')
    {
        throw "Invalid release version '$Value'. Use a stable x.y.z version, for example 1.1.0."
    }
    return [version]$Value
}

function Test-ReleaseNotes([string]$Path)
{
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $body = (Get-Content -LiteralPath $Path -Raw) -replace '(?m)^\s*#.*$', ''
    $body = $body -replace '(?s)<!--.*?-->', ''
    return $body -match '[\p{L}\p{N}]'
}

function Assert-ReleaseTag([string]$Revision)
{
    $tags = @(git -C $root ls-remote --tags origin "refs/tags/v$Version" "refs/tags/v$Version^{}")
    if ($LASTEXITCODE) { throw 'Could not check the release tag.' }
    if ($tags.Count)
    {
        $peeled = @($tags | Where-Object { $_.EndsWith('^{}') })
        $entry = if ($peeled.Count) { $peeled[0] } else { $tags[0] }
        if (($entry -split '\s+')[0] -ne $Revision)
        {
            throw "Tag v$Version points to a different commit. Choose a new version."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($KeyPath))
{
    throw 'Supply -KeyPath or set WEEKNUMBER_KEYPATH, then open a new terminal.'
}
if (-not (Test-Path -LiteralPath $KeyPath -PathType Leaf))
{
    throw 'The signing key file does not exist. Check -KeyPath or WEEKNUMBER_KEYPATH.'
}
$KeyPath = (Resolve-Path -LiteralPath $KeyPath).Path

# Failed lookups must never be mistaken for an unreleased version.
$releaseTags = @(gh api "repos/$repository/releases" --paginate --jq '.[].tag_name')
if ($LASTEXITCODE) { throw 'Could not query GitHub releases. Check gh authentication and connectivity.' }
$latest = [version]'0.0.0'
foreach ($tag in $releaseTags)
{
    if ($tag -cmatch '^v(\d+\.\d+\.\d+)$')
    {
        $publishedVersion = ConvertTo-ReleaseVersion $Matches[1]
        if ($publishedVersion -gt $latest) { $latest = $publishedVersion }
    }
}
$versionPath = Join-Path $root 'version.json'
$metadata = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json
if (-not $PSBoundParameters.ContainsKey('Version'))
{
    $Version = $metadata.version
    if ((ConvertTo-ReleaseVersion $Version) -le $latest)
    {
        $Version = Read-Host "Version to release (newer than $latest)"
    }
}
if ((ConvertTo-ReleaseVersion $Version) -le $latest)
{
    throw "Version $Version must be newer than the latest release $latest."
}
$branch = git -C $root branch --show-current
if ($LASTEXITCODE -or [string]::IsNullOrWhiteSpace($branch)) { throw 'Release from a checked-out branch, not a detached HEAD.' }
$revision = git -C $root rev-parse HEAD
if ($LASTEXITCODE) { throw 'Could not read the release commit.' }
Assert-ReleaseTag $revision

$notes = Join-Path $root "docs/releases/$Version.md"
if (-not (Test-ReleaseNotes $notes))
{
    $editor = Get-Command notepad++.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
    if (-not $editor)
    {
        $editor = @("$env:ProgramFiles\Notepad++\notepad++.exe", "${env:ProgramFiles(x86)}\Notepad++\notepad++.exe", "$env:LOCALAPPDATA\Programs\Notepad++\notepad++.exe") |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (-not $editor) { throw 'Notepad++ is required to write missing release notes. Install it or create the notes file first.' }
    if (-not (Test-Path -LiteralPath $notes) -or [string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $notes -Raw)))
    {
        New-Item -ItemType Directory -Path (Split-Path $notes -Parent) -Force | Out-Null
        [IO.File]::WriteAllText($notes, "# Voltura WeekNumber $Version`n`n")
    }
    Write-Host 'Write and save the release notes, then close the separate Notepad++ window to continue.'
    Start-Process -FilePath $editor -ArgumentList @('-multiInst', '-nosession', ('"' + $notes + '"')) -Wait
    if (-not (Test-ReleaseNotes $notes)) { throw 'Release notes require saved content beyond the heading. Nothing staged, committed, or pushed.' }
}
if ($metadata.version -ne $Version)
{
    $metadata.version = $Version
    [IO.File]::WriteAllText($versionPath, (($metadata | ConvertTo-Json -Compress) + "`n"))
}

# Validate the key, build/test once, and sign before committing or pushing.
& "$PSScriptRoot\sign-update.ps1" -KeyPath $KeyPath -BuildPackages -SkipTests:$NoTests
$assets = @(
    "VolturaWeekNumber-Setup-$Version-win-x64.exe",
    "VolturaWeekNumber-Setup-$Version-win-x64-full.exe",
    "VolturaWeekNumber-$Version-win-x64.zip",
    "VolturaWeekNumber-Update-$Version.json",
    "VolturaWeekNumber-Update-$Version.sig"
) | ForEach-Object { (Get-Item -LiteralPath (Join-Path $root "artifacts/publish/$_")).FullName }
if ($PrepareOnly)
{
    Write-Output 'Signed release prepared locally. Files retained; nothing staged, committed, pushed, or published.'
    return
}
git -C $root add -A
if ($LASTEXITCODE) { throw 'Could not stage release changes.' }
git -C $root diff --cached --quiet
if ($LASTEXITCODE -eq 1)
{
    git -C $root commit -m "Release $Version"
    if ($LASTEXITCODE) { throw 'Release commit failed. Fix the error and rerun the same command.' }
}
elseif ($LASTEXITCODE) { throw 'Could not inspect staged changes.' }
$revision = git -C $root rev-parse HEAD
if ($LASTEXITCODE) { throw 'Could not read the release commit.' }
$changes = @(git -C $root status --porcelain)
if ($LASTEXITCODE -or $changes.Count) { throw 'Files changed during release. Review the changes and rerun before publishing.' }
Assert-ReleaseTag $revision
git -C $root push origin "HEAD:refs/heads/$branch"
if ($LASTEXITCODE) { throw 'Release push failed. Resolve the Git error and rerun; no release was published.' }
gh api "repos/$repository/commits/$revision" --silent
if ($LASTEXITCODE) { throw 'Could not verify the pushed release commit.' }
gh release create "v$Version" @assets --repo $repository --target $revision --title "Voltura WeekNumber $Version" --notes-file $notes
if ($LASTEXITCODE) { throw 'Release publication failed. Inspect the GitHub release before retrying; the commit remains pushed.' }
