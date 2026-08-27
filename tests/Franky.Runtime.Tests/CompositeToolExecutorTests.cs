using Franky.Runtime.Tools;

namespace Franky.Runtime.Tests;

internal static class CompositeToolExecutorTests
{
    public static async Task RoutesCallsByExactToolName()
    {
        var first = new RecordingExecutor("first");
        var second = new RecordingExecutor("second");
        var composite = new CompositeToolExecutor(first, second);

        var result = await composite.ExecuteAsync(
            new ToolCall("second", "{}"),
            CancellationToken.None);

        TestAssert.True(result.Success);
        TestAssert.False(first.WasCalled);
        TestAssert.True(second.WasCalled);
        TestAssert.Equal(2, composite.ToolDefinitions.Count);
    }

    private sealed class RecordingExecutor(string name) : IToolExecutor
    {
        public bool WasCalled { get; private set; }

        public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } =
        [
            new(name, "test", new Dictionary<string, object?> { ["type"] = "object" }),
        ];

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolCall call,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new ToolExecutionResult(true, "{}", call.Name));
        }
    }
}
