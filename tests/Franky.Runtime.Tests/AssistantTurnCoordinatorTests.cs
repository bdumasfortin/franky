using Franky.Runtime.Conversation;
using Franky.Runtime.Diagnostics;

namespace Franky.Runtime.Tests;

internal static class AssistantTurnCoordinatorTests
{
    public static async Task PreservesOneConversationSession()
    {
        var client = new RecordingConversationClient();
        var coordinator = new AssistantTurnCoordinator(client, new RecordingEventSink());

        await coordinator.SendAsync("First", CancellationToken.None);
        await coordinator.SendAsync("Second", CancellationToken.None);

        TestAssert.Equal(2, client.Sessions.Count);
        TestAssert.True(ReferenceEquals(client.Sessions[0], client.Sessions[1]));
    }

    public static async Task RejectsOverlappingTurns()
    {
        var client = new BlockingConversationClient();
        var coordinator = new AssistantTurnCoordinator(client, new RecordingEventSink());
        var firstTurn = coordinator.SendAsync("First", CancellationToken.None);
        await client.Started.Task;

        try
        {
            await coordinator.SendAsync("Second", CancellationToken.None);
            throw new InvalidOperationException("The overlapping turn was not rejected.");
        }
        catch (AssistantTurnBusyException)
        {
        }
        finally
        {
            client.Release.SetResult();
            await firstTurn;
        }
    }

    private sealed class RecordingConversationClient : IConversationClient
    {
        public string ProviderName => "test";
        public List<ConversationSession> Sessions { get; } = [];

        public Task<AssistantReply> SendAsync(
            ConversationSession session,
            string userText,
            CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.FromResult(new AssistantReply("Done", 0, []));
        }
    }

    private sealed class BlockingConversationClient : IConversationClient
    {
        public string ProviderName => "test";
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AssistantReply> SendAsync(
            ConversationSession session,
            string userText,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new AssistantReply("Done", 0, []);
        }
    }

    private sealed class RecordingEventSink : IEventSink
    {
        public void Write(
            string eventName,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}
