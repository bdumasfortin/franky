namespace Franky.Runtime.Conversation;

public interface IConversationClient
{
    string ProviderName { get; }

    Task<AssistantReply> SendAsync(
        ConversationSession session,
        string userText,
        CancellationToken cancellationToken);
}
