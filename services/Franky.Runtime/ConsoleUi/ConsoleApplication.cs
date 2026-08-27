using System.Diagnostics;
using Franky.Runtime.Configuration;
using Franky.Runtime.Conversation;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

namespace Franky.Runtime.ConsoleUi;

public sealed class ConsoleApplication(
    IConversationClient conversationClient,
    IToolExecutor toolExecutor,
    IEventSink events,
    TextReader input,
    TextWriter output,
    AssistantOptions options)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await output.WriteLineAsync("Franky — text development console");
        await output.WriteLineAsync($"Provider: {conversationClient.ProviderName}");
        if (options.UseDemoProvider)
        {
            await output.WriteLineAsync("OPENAI_API_KEY is not configured or --demo was supplied; responses are local and deterministic.");
        }

        await output.WriteLineAsync("Commands: /help, /reset, /run system.identity, /run runtime.dotnet_version, /exit");

        var session = new ConversationSession();
        while (!cancellationToken.IsCancellationRequested)
        {
            await output.WriteAsync("you> ");
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null || string.Equals(line.Trim(), "/exit", StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("Goodbye.");
                return 0;
            }

            var command = line.Trim();
            if (command.Length == 0)
            {
                continue;
            }

            if (string.Equals(command, "/help", StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("Enter text to converse. /run accepts only the two displayed read-only command names.");
                continue;
            }

            if (string.Equals(command, "/reset", StringComparison.OrdinalIgnoreCase))
            {
                session.Reset();
                await output.WriteLineAsync("Conversation state reset.");
                continue;
            }

            if (command.StartsWith("/run ", StringComparison.OrdinalIgnoreCase))
            {
                await RunNamedCommandAsync(command[5..].Trim(), cancellationToken);
                continue;
            }

            var started = Stopwatch.GetTimestamp();
            try
            {
                var reply = await conversationClient.SendAsync(session, command, cancellationToken);
                events.Write("conversation.turn", new Dictionary<string, object?>
                {
                    ["success"] = true,
                    ["provider"] = conversationClient.ProviderName,
                    ["tool_calls"] = reply.ToolCallsExecuted,
                    ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                });
                await output.WriteLineAsync($"assistant> {reply.Text}");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                events.Write("conversation.turn", new Dictionary<string, object?>
                {
                    ["success"] = false,
                    ["provider"] = conversationClient.ProviderName,
                    ["error_type"] = exception.GetType().Name,
                    ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                });
                await output.WriteLineAsync($"assistant> I could not complete that request: {exception.Message}");
            }
        }

        return 0;
    }

    private async Task RunNamedCommandAsync(string commandName, CancellationToken cancellationToken)
    {
        var arguments = System.Text.Json.JsonSerializer.Serialize(new { command_name = commandName });
        var result = await toolExecutor.ExecuteAsync(
            new ToolCall(NamedCommandTool.ToolName, arguments),
            cancellationToken);
        events.Write("command.executed", new Dictionary<string, object?>
        {
            ["success"] = result.Success,
            ["command_name"] = commandName,
        });
        await output.WriteLineAsync($"tool> {result.OutputJson}");
    }
}
