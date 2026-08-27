namespace Franky.Runtime.Conversation;

public sealed record AssistantReply(
    string Text,
    int ToolCallsExecuted,
    IReadOnlyList<AssistantActionOutcome> Actions);

public sealed record AssistantActionOutcome(string Name, bool Success);
