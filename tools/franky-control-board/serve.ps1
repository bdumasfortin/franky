[CmdletBinding()]
param(
    [int]$Port = 8765
)

$ErrorActionPreference = 'Stop'
$taskUrl = "http://127.0.0.1:$Port"
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskRuntime = Join-Path $taskRepository 'services\Franky.Runtime'
Start-Process $taskUrl
dotnet run --project $taskRuntime -- --control-board --port $Port --web-root $PSScriptRoot
