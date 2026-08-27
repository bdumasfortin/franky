[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frankyWakeToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frankyRepoRoot = Resolve-Path (Join-Path $frankyWakeToolRoot '..\..')
$frankyModelSource = Join-Path $frankyWakeToolRoot '.cache\trained\yo_franky\tflite_stream_state_internal_quant\stream_state_internal_quant.tflite'
$frankyFirmwareModelRoot = Join-Path $frankyRepoRoot 'firmware\franky-device\main\models'
$frankyFirmwareModel = Join-Path $frankyFirmwareModelRoot 'yo_franky.tflite'

if (-not (Test-Path -LiteralPath $frankyModelSource)) {
    throw 'No trained quantized model was found. Run train.ps1 first.'
}

New-Item -ItemType Directory -Force -Path $frankyFirmwareModelRoot | Out-Null
Copy-Item -LiteralPath $frankyModelSource -Destination $frankyFirmwareModel -Force
$frankyHash = Get-FileHash -LiteralPath $frankyFirmwareModel -Algorithm SHA256

[pscustomobject]@{
    Model = $frankyFirmwareModel
    Bytes = (Get-Item -LiteralPath $frankyFirmwareModel).Length
    SHA256 = $frankyHash.Hash.ToLowerInvariant()
} | Format-List
