param(
    [Parameter(Mandatory)][string]$KeyPath,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content (Join-Path $root 'version.json') -Raw | ConvertFrom-Json).version
$notes = Join-Path $root "docs\releases\$version.md"

if ($Publish)
{
    if (-not (Test-Path -LiteralPath $notes))
    {
        throw 'Release notes are required before publication.'
    }

    $changes = git -C $root status --porcelain
    if ($LASTEXITCODE -or $changes)
    {
        throw 'Commit and push the release changes before publishing.'
    }

    $revision = git -C $root rev-parse HEAD
    if ($LASTEXITCODE) { throw 'Could not read the release commit.' }
    gh api "repos/voltura/voltura-weeknumber/commits/$revision" --silent
    if ($LASTEXITCODE) { throw 'Push the release commit before publishing.' }

    $remoteTags = @(git ls-remote --tags https://github.com/voltura/voltura-weeknumber.git "refs/tags/v$version" "refs/tags/v$version^{}")
    if ($LASTEXITCODE) { throw 'Could not check the release tag.' }
    if ($remoteTags.Count -gt 0)
    {
        $peeled = @($remoteTags | Where-Object { $_.EndsWith('^{}') })
        $tagCommit = if ($peeled.Count) { ($peeled[0] -split '\s+')[0] } else { ($remoteTags[0] -split '\s+')[0] }
        if ($tagCommit -ne $revision)
        {
            throw "Tag v$version points to a different commit. Correct the tag or choose a new version before publishing."
        }
    }
}

& "$PSScriptRoot\sign-update.ps1" -KeyPath $KeyPath -BuildPackages

if (-not $Publish)
{
    Write-Output 'Signed release prepared locally. Nothing published.'

    return
}

$assets = Get-ChildItem -LiteralPath (Join-Path $root 'artifacts\publish') -File |
    Where-Object { $_.Name -like "*-$version*" } |
    Select-Object -ExpandProperty FullName
gh release create "v$version" @assets --repo voltura/voltura-weeknumber --target $revision --title "Voltura WeekNumber $version" --notes-file $notes

if ($LASTEXITCODE)
{
    throw 'Release publication failed.'
}
