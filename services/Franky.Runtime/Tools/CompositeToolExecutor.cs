using System.Text.Json;

namespace Franky.Runtime.Tools;

public sealed class CompositeToolExecutor(params IToolExecutor[] executors) : IToolExecutor
{
    private readonly IReadOnlyDictionary<string, IToolExecutor> executorsByName = executors
        .SelectMany(executor => executor.ToolDefinitions.Select(definition => (definition.Name, Executor: executor)))
        .ToDictionary(item => item.Name, item => item.Executor, StringComparer.Ordinal);

    public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } = executors
        .SelectMany(executor => executor.ToolDefinitions)
        .ToArray();

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        CancellationToken cancellationToken)
    {
        if (executorsByName.TryGetValue(call.Name, out var executor))
        {
            return executor.ExecuteAsync(call, cancellationToken);
        }

        return Task.FromResult(new ToolExecutionResult(
            false,
            JsonSerializer.Serialize(new
            {
                success = false,
                error_code = "unknown_tool",
                error = $"Tool '{call.Name}' is not available.",
            }),
            call.Name));
    }
}
