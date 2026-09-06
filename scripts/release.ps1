param([Parameter(Mandatory)][string]$KeyPath, [switch]$Publish)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\package.ps1"
& "$PSScriptRoot\sign-update.ps1" -KeyPath $KeyPath
if (-not $Publish) { Write-Output 'Signed release prepared locally. Nothing published.'; return }
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content (Join-Path $root 'version.json') -Raw | ConvertFrom-Json).version
$notes = Join-Path $root "docs\releases\$version.md"
if (-not (Test-Path -LiteralPath $notes)) { throw 'Release notes are required before publication.' }
$assets = Get-ChildItem -LiteralPath (Join-Path $root 'artifacts\publish') -File | Where-Object { $_.Name -like "*-$version*" } | Select-Object -ExpandProperty FullName
gh release create "v$version" @assets --repo voltura/voltura-weeknumber --verify-tag --title "Voltura WeekNumber $version" --notes-file $notes
if ($LASTEXITCODE) { throw 'Release publication failed.' }
