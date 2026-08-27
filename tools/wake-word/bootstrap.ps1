[CmdletBinding()]
param(
    [switch]$CpuOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frankyWakeToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frankyVenvRoot = Join-Path $frankyWakeToolRoot '.venv'
$frankyPython = Join-Path $frankyVenvRoot 'Scripts\python.exe'
$frankyCacheRoot = Join-Path $frankyWakeToolRoot '.cache'
$frankyVendorRoot = Join-Path $frankyCacheRoot 'vendor'
$frankyPiperSource = Join-Path $frankyVendorRoot 'piper-sample-generator'
$frankyMicroWakeWordSource = Join-Path $frankyVendorRoot 'micro-wake-word'
$frankyModelRoot = Join-Path $frankyCacheRoot 'models'
$frankyGeneratorModel = Join-Path $frankyModelRoot 'en_US-libritts_r-medium.pt'
$frankyGeneratorConfig = $frankyGeneratorModel + '.json'

if ($IsWindows) {
    $frankyWinGetPackages = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $frankyFfmpegSharedBin = Get-ChildItem -LiteralPath $frankyWinGetPackages -Directory -Filter 'Gyan.FFmpeg.Shared*' -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Recurse -Filter bin -ErrorAction SilentlyContinue } |
        Where-Object { $_.Parent.Name -match '^ffmpeg-[4-8]\..*-full_build-shared$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $frankyFfmpegSharedBin) {
        throw 'FFmpeg Shared 8 is required for dataset decoding. Install it with: winget install --id Gyan.FFmpeg.Shared --exact --version 8.1.2'
    }
    $env:Path = $frankyFfmpegSharedBin.FullName + [IO.Path]::PathSeparator + $env:Path
}

if (-not (Test-Path -LiteralPath $frankyPython)) {
    $frankyPython310 = & py -3.10 -c 'import sys; print(sys.executable)'
    if ($LASTEXITCODE -ne 0 -or -not $frankyPython310) {
        throw 'Python 3.10 is required. Install the Python.Python.3.10 package, then rerun this script.'
    }

    & $frankyPython310 -m venv $frankyVenvRoot
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the wake-word virtual environment.' }
}

& $frankyPython -m pip install --upgrade pip "setuptools<82" wheel
if ($LASTEXITCODE -ne 0) { throw 'Could not update Python packaging tools.' }

& $frankyPython -m pip install --requirement (Join-Path $frankyWakeToolRoot 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Could not install the wake-word training packages.' }

$frankyTorchBuild = & $frankyPython -c "import torch; print(torch.__version__)" 2>$null
$frankyExpectedTorchBuild = if ($CpuOnly) { '2.11.0+cpu' } else { '2.11.0+cu130' }
if ($frankyTorchBuild -ne $frankyExpectedTorchBuild) {
    if ($CpuOnly) {
        & $frankyPython -m pip install --force-reinstall torch==2.11.0 torchaudio==2.11.0 --index-url https://download.pytorch.org/whl/cpu
    } else {
        & $frankyPython -m pip install --force-reinstall torch==2.11.0 torchaudio==2.11.0 --index-url https://download.pytorch.org/whl/cu130
    }
    if ($LASTEXITCODE -ne 0) { throw 'Could not install the selected PyTorch runtime.' }
}

# PyTorch's dependency resolver may select a newer fsspec than datasets accepts.
& $frankyPython -m pip install fsspec==2026.6.0
if ($LASTEXITCODE -ne 0) { throw 'Could not restore the pinned fsspec version.' }

New-Item -ItemType Directory -Force -Path $frankyVendorRoot,$frankyModelRoot | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $frankyMicroWakeWordSource '.git'))) {
    git clone https://github.com/OHF-Voice/micro-wake-word.git $frankyMicroWakeWordSource
    if ($LASTEXITCODE -ne 0) { throw 'Could not clone microWakeWord.' }
}
git -C $frankyMicroWakeWordSource fetch --depth 1 origin 4665173cd35f1cff9a61e06fc427f124766c488e
git -C $frankyMicroWakeWordSource checkout --detach 4665173cd35f1cff9a61e06fc427f124766c488e
if ($LASTEXITCODE -ne 0) { throw 'Could not select the pinned microWakeWord revision.' }

if (-not (Test-Path -LiteralPath (Join-Path $frankyPiperSource '.git'))) {
    git clone https://github.com/rhasspy/piper-sample-generator.git $frankyPiperSource
    if ($LASTEXITCODE -ne 0) { throw 'Could not clone Piper Sample Generator.' }
}
git -C $frankyPiperSource fetch --depth 1 origin 2971426a55072f7d22fec416ca7800df8bd23207
git -C $frankyPiperSource checkout --detach 2971426a55072f7d22fec416ca7800df8bd23207
if ($LASTEXITCODE -ne 0) { throw 'Could not select the pinned Piper Sample Generator revision.' }

if (-not (Test-Path -LiteralPath $frankyGeneratorModel)) {
    Invoke-WebRequest `
        -Uri 'https://github.com/rhasspy/piper-sample-generator/releases/download/v2.0.0/en_US-libritts_r-medium.pt' `
        -OutFile $frankyGeneratorModel
}
if (-not (Test-Path -LiteralPath $frankyGeneratorConfig)) {
    Copy-Item `
        -LiteralPath (Join-Path $frankyPiperSource 'models\en_US-libritts_r-medium.pt.json') `
        -Destination $frankyGeneratorConfig
}

& $frankyPython -m pip check
if ($LASTEXITCODE -ne 0) { throw 'The wake-word environment contains incompatible packages.' }

& $frankyPython -c "import tensorflow as tf, torch, torchcodec; print('TensorFlow', tf.__version__); print('PyTorch', torch.__version__); print('TorchCodec', torchcodec.__version__); print('CUDA sample generation', torch.cuda.is_available())"
