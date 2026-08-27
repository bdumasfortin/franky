[CmdletBinding()]
param(
    [switch]$Fresh
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frankyWakeToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frankyPython = Join-Path $frankyWakeToolRoot '.venv\Scripts\python.exe'
$frankyConfig = Join-Path $frankyWakeToolRoot 'training_parameters.yaml'
$frankyTrainRoot = Join-Path $frankyWakeToolRoot '.cache\trained\yo_franky'

if (-not (Test-Path -LiteralPath $frankyPython)) {
    throw 'Wake-word environment is missing. Run bootstrap.ps1 first.'
}
if ($Fresh -and (Test-Path -LiteralPath $frankyTrainRoot)) {
    throw 'Fresh training would remove an existing model. Move the .cache/trained/yo_franky directory yourself, then rerun.'
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
    if ($LASTEXITCODE -ne 0) { throw 'Wake-word training failed.' }
} finally {
    $env:PYTHONPATH = $frankyPreviousPythonPath
    Pop-Location
}
