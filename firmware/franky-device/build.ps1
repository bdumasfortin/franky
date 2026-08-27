[CmdletBinding()]
param()

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
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'sdkconfig'))) {
        & $taskEim run 'idf.py set-target esp32s3' v5.5.2
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    & $taskEim run 'idf.py build' v5.5.2
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
