using System.Text.Json;

namespace Franky.Runtime.Tools;

public sealed class DeviceActionTool : IToolExecutor
{
    public const string ToolName = "request_device_action";
    public const string FrankySuuuperAction = "device.sfx.frankys_suuuper";

    private static readonly string[] Actions = [FrankySuuuperAction];

    public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } =
    [
        new(
            ToolName,
            "Requests one fixed action on the connected Franky device. Use device.sfx.frankys_suuuper whenever the user asks Franky how it is going, how Franky is doing, or asks Franky to play or say SUUUPER. This result only queues the device request; it does not confirm that playback finished.",
            new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["action_name"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "The exact allowlisted device action to request.",
                        ["enum"] = Actions,
                    },
                },
                ["required"] = new[] { "action_name" },
                ["additionalProperties"] = false,
            }),
    ];

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(call.Name, ToolName, StringComparison.Ordinal))
        {
            return Task.FromResult(Failure(
                "unknown_tool",
                $"Tool '{call.Name}' is not available.",
                call.Name));
        }

        string? actionName;
        try
        {
            using var arguments = JsonDocument.Parse(call.ArgumentsJson);
            actionName = arguments.RootElement.TryGetProperty("action_name", out var actionElement)
                ? actionElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return Task.FromResult(Failure(
                "invalid_arguments",
                "Tool arguments were not valid JSON.",
                ToolName));
        }

        if (string.IsNullOrWhiteSpace(actionName) ||
            !Actions.Contains(actionName, StringComparer.Ordinal))
        {
            return Task.FromResult(Failure(
                "action_not_allowed",
                "The requested device action is not on the application allowlist.",
                actionName ?? ToolName));
        }

        return Task.FromResult(new ToolExecutionResult(
            true,
            JsonSerializer.Serialize(new
            {
                success = true,
                action_name = actionName,
                status = "queued_for_device",
                message = "The browser will request playback and wait for the board acknowledgement.",
            }),
            actionName));
    }

    private static ToolExecutionResult Failure(string code, string message, string actionName) =>
        new(
            false,
            JsonSerializer.Serialize(new { success = false, error_code = code, error = message }),
            actionName);
}
