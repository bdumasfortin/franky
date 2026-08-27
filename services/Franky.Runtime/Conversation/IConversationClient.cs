namespace Franky.Runtime.Conversation;

public interface IConversationClient
{
    string ProviderName { get; }

    Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task<AssistantReply> SendAsync(
        ConversationSession session,
        string userText,
        CancellationToken cancellationToken);
}
