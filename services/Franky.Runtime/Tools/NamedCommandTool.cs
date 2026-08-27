using System.Text.Json;

namespace Franky.Runtime.Tools;

public sealed class NamedCommandTool(IProcessRunner processRunner) : IToolExecutor
{
    public const string ToolName = "run_named_command";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    private static readonly IReadOnlyDictionary<string, ProcessSpec> Commands =
        new Dictionary<string, ProcessSpec>(StringComparer.Ordinal)
        {
            ["system.identity"] = new(
                OperatingSystem.IsWindows() ? "whoami.exe" : "/usr/bin/whoami",
                [],
                CommandTimeout,
                MaximumOutputCharacters: 4_096),
            ["runtime.dotnet_version"] = new(
                "dotnet",
                ["--version"],
                CommandTimeout,
                MaximumOutputCharacters: 4_096),
        };

    public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } =
    [
        new(
            ToolName,
            "Runs one read-only command selected from a fixed application allowlist. It cannot run arbitrary shell text or arguments.",
            new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["command_name"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The exact allowlisted command to run. system.identity reports the operating-system account running Franky; runtime.dotnet_version reports the installed .NET SDK version.",
                        ["enum"] = Commands.Keys.Order(StringComparer.Ordinal).ToArray(),
                    },
                },
                ["required"] = new[] { "command_name" },
                ["additionalProperties"] = false,
            }),
    ];

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(call.Name, ToolName, StringComparison.Ordinal))
        {
            return Failure("unknown_tool", $"Tool '{call.Name}' is not available.", call.Name);
        }

        string? commandName;
        try
        {
            using var arguments = JsonDocument.Parse(call.ArgumentsJson);
            commandName = arguments.RootElement.TryGetProperty("command_name", out var commandElement)
                ? commandElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return Failure("invalid_arguments", "Tool arguments were not valid JSON.", ToolName);
        }

        if (string.IsNullOrWhiteSpace(commandName) || !Commands.TryGetValue(commandName, out var process))
        {
            return Failure(
                "command_not_allowed",
                "The requested command is not on the application allowlist.",
                commandName ?? ToolName);
        }

        var result = await processRunner.RunAsync(process, cancellationToken);
        return new ToolExecutionResult(
            result.Success,
            JsonSerializer.Serialize(new
            {
                success = result.Success,
                command_name = commandName,
                exit_code = result.ExitCode,
                stdout = result.StandardOutput,
                stderr = result.StandardError,
                error = result.Error,
            }),
            commandName);
    }

    private static ToolExecutionResult Failure(string code, string message, string actionName) =>
        new(
            false,
            JsonSerializer.Serialize(new { success = false, error_code = code, error = message }),
            actionName);
}
