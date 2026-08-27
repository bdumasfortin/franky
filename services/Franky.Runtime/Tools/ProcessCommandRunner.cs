using System.Diagnostics;

namespace Franky.Runtime.Tools;

public sealed class ProcessCommandRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessSpec process,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(process.Timeout);

        var startInfo = new ProcessStartInfo
        {
            FileName = process.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in process.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var child = new Process { StartInfo = startInfo };
        try
        {
            if (!child.Start())
            {
                return new ProcessResult(false, null, "", "", "The process did not start.");
            }

            var stdoutTask = child.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = child.StandardError.ReadToEndAsync(timeout.Token);
            await child.WaitForExitAsync(timeout.Token);
            var stdout = Limit(await stdoutTask, process.MaximumOutputCharacters);
            var stderr = Limit(await stderrTask, process.MaximumOutputCharacters);
            return new ProcessResult(
                child.ExitCode == 0,
                child.ExitCode,
                stdout,
                stderr,
                child.ExitCode == 0 ? null : "The command returned a non-zero exit code.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(child);
            return new ProcessResult(false, null, "", "", $"The command exceeded its {process.Timeout.TotalSeconds:0}-second timeout.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TryKill(child);
            return new ProcessResult(false, null, "", "", exception.Message);
        }
    }

    private static string Limit(string value, int maximumCharacters) =>
        value.Length <= maximumCharacters ? value.Trim() : value[..maximumCharacters].Trim() + "…";

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process was never started or exited between checks.
        }
    }
}
