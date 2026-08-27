namespace Franky.Runtime.Conversation;

public sealed class DemoConversationClient : IConversationClient
{
    public string ProviderName => "local demo";

    public Task<AssistantReply> SendAsync(
        ConversationSession session,
        string userText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reply = $"[demo] I received: {userText}";
        return Task.FromResult(new AssistantReply(reply, ToolCallsExecuted: 0, Actions: []));
    }
}
