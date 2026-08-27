namespace Franky.Runtime.Tools;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec process, CancellationToken cancellationToken);
}

public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout,
    int MaximumOutputCharacters);

public sealed record ProcessResult(
    bool Success,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string? Error);
