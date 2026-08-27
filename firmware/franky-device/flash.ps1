[CmdletBinding()]
param(
    [string]$Port = 'COM5'
)

$ErrorActionPreference = 'Stop'
$taskEim = (Get-Command eim -ErrorAction SilentlyContinue).Source
if (-not $taskEim) {
    $taskEim = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\eim.exe'
}
if (-not (Test-Path -LiteralPath $taskEim)) {
    throw 'Espressif Installation Manager (eim) was not found.'
}

Push-Location $PSScriptRoot
try {
    & $taskEim run "idf.py -p $Port flash" v5.5.2
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
