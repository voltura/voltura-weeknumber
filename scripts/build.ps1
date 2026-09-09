$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try
{
    dotnet build VolturaWeekNumber.slnx -c Release -p:RestoreLockedMode=true

    if ($LASTEXITCODE)
    {
        throw 'Build failed.'
    }

    dotnet test --solution VolturaWeekNumber.slnx -c Release --no-build

    if ($LASTEXITCODE)
    {
        throw 'Tests failed.'
    }
}
finally
{
    Pop-Location
}
