using System.Diagnostics;
using Franky.Runtime.Diagnostics;

namespace Franky.Runtime.Conversation;

public sealed class AssistantTurnCoordinator(
    IConversationClient conversationClient,
    IEventSink events)
{
    private readonly SemaphoreSlim turnGate = new(1, 1);
    private readonly ConversationSession session = new();

    public string ProviderName => conversationClient.ProviderName;

    public async Task<AssistantReply> SendAsync(
        string userText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        if (!await turnGate.WaitAsync(0, cancellationToken))
        {
            throw new AssistantTurnBusyException();
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var reply = await conversationClient.SendAsync(session, userText, cancellationToken);
            events.Write("conversation.turn", new Dictionary<string, object?>
            {
                ["success"] = true,
                ["provider"] = conversationClient.ProviderName,
                ["tool_calls"] = reply.ToolCallsExecuted,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });
            return reply;
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
            throw;
        }
        finally
        {
            turnGate.Release();
        }
    }
}

public sealed class AssistantTurnBusyException()
    : Exception("Franky is already processing another request.");
