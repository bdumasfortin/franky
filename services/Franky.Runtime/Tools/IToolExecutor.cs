namespace Franky.Runtime.Tools;

public interface IToolExecutor
{
    IReadOnlyList<ToolDefinition> ToolDefinitions { get; }

    Task<ToolExecutionResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken);
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyDictionary<string, object?> Parameters,
    bool Strict = true);

public sealed record ToolCall(string Name, string ArgumentsJson);

public sealed record ToolExecutionResult(
    bool Success,
    string OutputJson,
    string? ActionName = null);
