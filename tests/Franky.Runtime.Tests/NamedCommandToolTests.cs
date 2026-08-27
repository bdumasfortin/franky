using Franky.Runtime.Tools;

namespace Franky.Runtime.Tests;

internal static class NamedCommandToolTests
{
    public static async Task RejectsCommandOutsideAllowlist()
    {
        var runner = new RecordingProcessRunner();
        var tool = new NamedCommandTool(runner);

        var result = await tool.ExecuteAsync(
            new ToolCall(NamedCommandTool.ToolName, "{\"command_name\":\"format.disk\"}"),
            CancellationToken.None);

        TestAssert.False(result.Success);
        TestAssert.Contains("not on the application allowlist", result.OutputJson);
        TestAssert.Null(runner.LastSpec);
    }

    public static async Task MapsAllowedNameToFixedProcess()
    {
        var runner = new RecordingProcessRunner
        {
            Result = new ProcessResult(true, 0, "10.0.301", string.Empty, null),
        };
        var tool = new NamedCommandTool(runner);

        var result = await tool.ExecuteAsync(
            new ToolCall(NamedCommandTool.ToolName, "{\"command_name\":\"runtime.dotnet_version\"}"),
            CancellationToken.None);

        TestAssert.True(result.Success);
        TestAssert.NotNull(runner.LastSpec);
        TestAssert.Equal("dotnet", runner.LastSpec!.FileName);
        TestAssert.SequenceEqual(new[] { "--version" }, runner.LastSpec.Arguments);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public ProcessSpec? LastSpec { get; private set; }

        public ProcessResult Result { get; init; } = new(true, 0, string.Empty, string.Empty, null);

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            LastSpec = spec;
            return Task.FromResult(Result);
        }
    }
}
