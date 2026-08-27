[CmdletBinding()]
param(
    [int]$Port = 8765,
    [ValidateSet('ollama', 'openai', 'demo')]
    [string]$AssistantProvider = 'ollama',
    [string]$AssistantModel
)

$ErrorActionPreference = 'Stop'
$taskUrl = "http://127.0.0.1:$Port"
$taskRepository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$taskRuntime = Join-Path $taskRepository 'services\Franky.Runtime'
$env:FRANKY_ASSISTANT_PROVIDER = $AssistantProvider
if ($AssistantModel) {
    if ($AssistantProvider -eq 'ollama') {
        $env:FRANKY_OLLAMA_MODEL = $AssistantModel
    } elseif ($AssistantProvider -eq 'openai') {
        $env:FRANKY_OPENAI_MODEL = $AssistantModel
    }
}
Start-Process $taskUrl
dotnet run --project $taskRuntime -- --control-board --port $Port --web-root $PSScriptRoot
