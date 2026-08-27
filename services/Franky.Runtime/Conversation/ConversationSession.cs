namespace Franky.Runtime.Conversation;

public sealed class ConversationSession
{
    public string? PreviousResponseId { get; set; }
    internal List<LocalConversationMessage> LocalMessages { get; } = [];

    public void Reset()
    {
        PreviousResponseId = null;
        LocalMessages.Clear();
    }

    internal void ReplaceLocalMessages(IEnumerable<LocalConversationMessage> messages)
    {
        LocalMessages.Clear();
        LocalMessages.AddRange(messages);
    }
}

internal sealed record LocalConversationMessage(
    string Role,
    string? Content = null,
    IReadOnlyList<LocalConversationToolCall>? ToolCalls = null,
    string? ToolName = null);

internal sealed record LocalConversationToolCall(
    string Name,
    string ArgumentsJson);
