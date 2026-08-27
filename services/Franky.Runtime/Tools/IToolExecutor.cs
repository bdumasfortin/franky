namespace Franky.Runtime.Tools;

public interface IToolExecutor
{
    IReadOnlyList<object> OpenAiToolDefinitions { get; }

    Task<ToolExecutionResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken);
}

public sealed record ToolCall(string Name, string ArgumentsJson);

public sealed record ToolExecutionResult(bool Success, string OutputJson);
