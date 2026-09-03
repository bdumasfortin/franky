[CmdletBinding()]
param(
    [ValidateSet('v1', 'v2')]
    [string]$Candidate = 'v2'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frankyWakeToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frankyPython = Join-Path $frankyWakeToolRoot '.venv\Scripts\python.exe'
$frankyConfig = Join-Path $frankyWakeToolRoot "training_parameters_physical_candidate_$Candidate.yaml"
$frankyBaselineRoot = Join-Path $frankyWakeToolRoot '.cache\trained\yo_franky'
$frankyCandidateRoot = Join-Path $frankyWakeToolRoot ".cache\trained\yo_franky_physical_$Candidate"
$frankyPhysicalManifest = Join-Path $frankyWakeToolRoot ".cache\features\physical-corpus-$Candidate\manifest.json"
$frankyBaselineModel = Join-Path $frankyBaselineRoot 'tflite_stream_state_internal_quant\stream_state_internal_quant.tflite'
$frankyExpectedBaselineHash = '987223A0697B9F8A382F6F00CC523026478BA99A21CEF264E3686FD887B203DD'

if (-not (Test-Path -LiteralPath $frankyPython)) {
    throw 'Wake-word environment is missing. Run bootstrap.ps1 first.'
}
if (-not (Test-Path -LiteralPath $frankyPhysicalManifest)) {
    throw "Physical corpus features are missing. Run prepare_dataset.py --only-physical-corpus --physical-candidate $Candidate first."
}
if (-not (Test-Path -LiteralPath (Join-Path $frankyBaselineRoot 'restore\checkpoint'))) {
    throw 'The baseline training checkpoint is missing.'
}
if (-not (Test-Path -LiteralPath $frankyBaselineModel)) {
    throw 'The rollback baseline model is missing.'
}
$frankyBaselineHash = (Get-FileHash -LiteralPath $frankyBaselineModel -Algorithm SHA256).Hash
if ($frankyBaselineHash -ne $frankyExpectedBaselineHash) {
    throw "The rollback baseline model hash is $frankyBaselineHash; expected $frankyExpectedBaselineHash."
}

if (-not (Test-Path -LiteralPath $frankyCandidateRoot)) {
    $frankyCandidateRestore = Join-Path $frankyCandidateRoot 'restore'
    New-Item -ItemType Directory -Path $frankyCandidateRestore -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $frankyBaselineRoot 'restore') -File |
        Copy-Item -Destination $frankyCandidateRestore
} elseif (-not (Test-Path -LiteralPath (Join-Path $frankyCandidateRoot 'restore\checkpoint'))) {
    throw 'The candidate directory is partial. Move it aside before rerunning.'
}

Push-Location $frankyWakeToolRoot
try {
    $env:TF_CPP_MIN_LOG_LEVEL = '1'
    $frankyPreviousPythonPath = $env:PYTHONPATH
    $frankyMicroWakeWordSource = Join-Path $frankyWakeToolRoot '.cache\vendor\micro-wake-word'
    $env:PYTHONPATH = if ($frankyPreviousPythonPath) {
        $frankyMicroWakeWordSource + [IO.Path]::PathSeparator + $frankyPreviousPythonPath
    } else {
        $frankyMicroWakeWordSource
    }
    & $frankyPython -m microwakeword.model_train_eval `
        --training_config=$frankyConfig `
        --train 1 `
        --restore_checkpoint 1 `
        --test_tf_nonstreaming 0 `
        --test_tflite_nonstreaming 0 `
        --test_tflite_nonstreaming_quantized 0 `
        --test_tflite_streaming 0 `
        --test_tflite_streaming_quantized 1 `
        --use_weights best_weights `
        mixednet `
        --pointwise_filters '64,64,64,64' `
        --repeat_in_block '1,1,1,1' `
        --mixconv_kernel_sizes '[5], [7,11], [9,15], [23]' `
        --residual_connection '0,0,0,0' `
        --first_conv_filters 32 `
        --first_conv_kernel_size 5 `
        --stride 3
    if ($LASTEXITCODE -ne 0) { throw 'Physical wake-word candidate training failed.' }
} finally {
    $env:PYTHONPATH = $frankyPreviousPythonPath
    Pop-Location
}

$frankyCandidateModel = Join-Path $frankyCandidateRoot 'tflite_stream_state_internal_quant\stream_state_internal_quant.tflite'
if (-not (Test-Path -LiteralPath $frankyCandidateModel)) {
    throw 'Candidate training completed without a quantized streaming model.'
}
$frankyCandidateHash = Get-FileHash -LiteralPath $frankyCandidateModel -Algorithm SHA256
[pscustomobject]@{
    Candidate = $frankyCandidateModel
    Bytes = (Get-Item -LiteralPath $frankyCandidateModel).Length
    SHA256 = $frankyCandidateHash.Hash.ToLowerInvariant()
    BaselineSHA256 = $frankyBaselineHash.ToLowerInvariant()
} | Format-List
