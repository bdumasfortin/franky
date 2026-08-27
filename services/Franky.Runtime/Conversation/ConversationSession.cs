namespace Franky.Runtime.Conversation;

public sealed class ConversationSession
{
    public string? PreviousResponseId { get; set; }

    public void Reset() => PreviousResponseId = null;
}
