using System.Net;
using System.Text;
using System.Text.Json;
using Franky.Runtime.Conversation;
using Franky.Runtime.Configuration;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

namespace Franky.Runtime.Tests;

internal static class OpenAiResponsesClientTests
{
    public static async Task ReturnsTextAndStoresContinuationId()
    {
        var handler = new QueueHttpMessageHandler(
            """
            {
              "id": "resp_1",
              "output": [
                {
                  "type": "message",
                  "content": [{ "type": "output_text", "text": "Hello there." }]
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        var client = new OpenAiResponsesClient(
            httpClient,
            TestOptions(),
            new RejectingToolExecutor(),
            new RecordingEventSink());
        var session = new ConversationSession();

        var reply = await client.SendAsync(session, "Hello", CancellationToken.None);

        TestAssert.Equal("Hello there.", reply.Text);
        TestAssert.Equal("resp_1", session.PreviousResponseId);
        TestAssert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        TestAssert.Equal("test-key", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    public static async Task ExecutesToolAndReturnsToolOutputToModel()
    {
        var handler = new QueueHttpMessageHandler(
            """
            {
              "id": "resp_tool",
              "output": [
                {
                  "type": "function_call",
                  "call_id": "call_1",
                  "name": "run_named_command",
                  "arguments": "{\"command\":\"runtime.dotnet_version\"}"
                }
              ]
            }
            """,
            """
            {
              "id": "resp_final",
              "output": [
                {
                  "type": "message",
                  "content": [{ "type": "output_text", "text": "The runtime is available." }]
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        var toolExecutor = new RecordingToolExecutor();
        var client = new OpenAiResponsesClient(
            httpClient,
            TestOptions(),
            toolExecutor,
            new RecordingEventSink());
        var session = new ConversationSession();

        var reply = await client.SendAsync(session, "Check the runtime", CancellationToken.None);

        TestAssert.Equal("The runtime is available.", reply.Text);
        TestAssert.Equal("resp_final", session.PreviousResponseId);
        TestAssert.Equal(1, toolExecutor.CallCount);
        TestAssert.Equal(2, handler.Requests.Count);

        var followUpJson = await handler.Requests[1].Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(followUpJson);
        var root = document.RootElement;
        TestAssert.Equal("resp_tool", root.GetProperty("previous_response_id").GetString());
        var toolOutput = root.GetProperty("input")[0];
        TestAssert.Equal("function_call_output", toolOutput.GetProperty("type").GetString());
        TestAssert.Equal("call_1", toolOutput.GetProperty("call_id").GetString());
        TestAssert.Contains("10.0.301", toolOutput.GetProperty("output").GetString()!);
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content is null
                    ? null
                    : new StringContent(
                        await request.Content.ReadAsStringAsync(cancellationToken),
                        Encoding.UTF8,
                        "application/json"),
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Requests.Add(clone);

            if (responses.Count == 0)
            {
                throw new InvalidOperationException("The test did not configure another HTTP response.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class RecordingToolExecutor : IToolExecutor
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<object> OpenAiToolDefinitions { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new ToolExecutionResult(true, "{\"stdout\":\"10.0.301\"}"));
        }
    }

    private sealed class RejectingToolExecutor : IToolExecutor
    {
        public IReadOnlyList<object> OpenAiToolDefinitions { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No tool call was expected in this test.");
    }

    private sealed class RecordingEventSink : IEventSink
    {
        public void Write(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }

    private static AssistantOptions TestOptions() =>
        new(
            UseDemoProvider: false,
            OpenAiApiKey: "test-key",
            OpenAiModel: "gpt-test",
            OpenAiBaseUri: new Uri("https://api.openai.com/"),
            MaxToolRounds: 2);
}
