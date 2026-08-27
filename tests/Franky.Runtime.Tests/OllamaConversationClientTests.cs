using System.Net;
using System.Text;
using System.Text.Json;
using Franky.Runtime.Configuration;
using Franky.Runtime.Conversation;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

namespace Franky.Runtime.Tests;

internal static class OllamaConversationClientTests
{
    public static async Task PreservesConversationLocally()
    {
        var handler = new QueueHttpMessageHandler(
            """
            { "model": "qwen3.5:4b", "done": true }
            """,
            """
            { "message": { "role": "assistant", "content": "Hello there." }, "done": true }
            """,
            """
            { "message": { "role": "assistant", "content": "Still here." }, "done": true }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var client = new OllamaConversationClient(
            httpClient,
            TestOptions(),
            new RejectingToolExecutor(),
            new RecordingEventSink());
        var session = new ConversationSession();

        await client.SendAsync(session, "Hello", CancellationToken.None);
        var reply = await client.SendAsync(session, "Are you there?", CancellationToken.None);

        TestAssert.Equal("Still here.", reply.Text);
        TestAssert.Equal(3, handler.Requests.Count);
        TestAssert.Equal("/api/generate", handler.Requests[0].RequestUri?.AbsolutePath);
        using var preload = JsonDocument.Parse(await handler.Requests[0].Content!.ReadAsStringAsync());
        TestAssert.Equal("1h", preload.RootElement.GetProperty("keep_alive").GetString());
        using var secondRequest = JsonDocument.Parse(await handler.Requests[2].Content!.ReadAsStringAsync());
        var messages = secondRequest.RootElement.GetProperty("messages");
        TestAssert.Equal(4, messages.GetArrayLength());
        TestAssert.Equal("system", messages[0].GetProperty("role").GetString());
        TestAssert.Equal("Hello", messages[1].GetProperty("content").GetString());
        TestAssert.Equal("Hello there.", messages[2].GetProperty("content").GetString());
        TestAssert.Equal("Are you there?", messages[3].GetProperty("content").GetString());
        TestAssert.Equal("qwen3.5:4b", secondRequest.RootElement.GetProperty("model").GetString());
        TestAssert.False(secondRequest.RootElement.GetProperty("think").GetBoolean());
    }

    public static async Task ExecutesToolAndReturnsToolOutputToModel()
    {
        var handler = new QueueHttpMessageHandler(
            """
            { "model": "qwen3.5:4b", "done": true }
            """,
            """
            {
              "message": {
                "role": "assistant",
                "content": "",
                "tool_calls": [
                  {
                    "type": "function",
                    "function": {
                      "name": "run_named_command",
                      "arguments": { "command_name": "runtime.dotnet_version" }
                    }
                  }
                ]
              },
              "done": true
            }
            """,
            """
            { "message": { "role": "assistant", "content": "The runtime is available." }, "done": true }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var toolExecutor = new RecordingToolExecutor();
        var client = new OllamaConversationClient(
            httpClient,
            TestOptions(),
            toolExecutor,
            new RecordingEventSink());

        var reply = await client.SendAsync(
            new ConversationSession(),
            "Check the runtime",
            CancellationToken.None);

        TestAssert.Equal("The runtime is available.", reply.Text);
        TestAssert.Equal(1, toolExecutor.CallCount);
        TestAssert.Equal(1, reply.Actions.Count);
        TestAssert.Equal("runtime.dotnet_version", reply.Actions[0].Name);
        TestAssert.True(reply.Actions[0].Success);
        TestAssert.Equal(3, handler.Requests.Count);

        using var firstRequest = JsonDocument.Parse(await handler.Requests[1].Content!.ReadAsStringAsync());
        var function = firstRequest.RootElement.GetProperty("tools")[0].GetProperty("function");
        TestAssert.Equal("run_named_command", function.GetProperty("name").GetString());

        using var followUp = JsonDocument.Parse(await handler.Requests[2].Content!.ReadAsStringAsync());
        var messages = followUp.RootElement.GetProperty("messages");
        var toolOutput = messages[messages.GetArrayLength() - 1];
        TestAssert.Equal("tool", toolOutput.GetProperty("role").GetString());
        TestAssert.Equal("run_named_command", toolOutput.GetProperty("tool_name").GetString());
        TestAssert.Contains("10.0.301", toolOutput.GetProperty("content").GetString()!);
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

        public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } =
        [
            new(
                "run_named_command",
                "Runs one allowlisted command.",
                new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>(),
                }),
        ];

        public Task<ToolExecutionResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
        {
            CallCount++;
            using var arguments = JsonDocument.Parse(call.ArgumentsJson);
            TestAssert.Equal(
                "runtime.dotnet_version",
                arguments.RootElement.GetProperty("command_name").GetString());
            return Task.FromResult(new ToolExecutionResult(
                true,
                "{\"stdout\":\"10.0.301\"}",
                "runtime.dotnet_version"));
        }
    }

    private sealed class RejectingToolExecutor : IToolExecutor
    {
        public IReadOnlyList<ToolDefinition> ToolDefinitions { get; } = [];

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
            OpenAiApiKey: null,
            OpenAiModel: "gpt-test",
            OpenAiBaseUri: new Uri("https://api.openai.com/"),
            MaxToolRounds: 2)
        {
            Provider = AssistantProvider.Ollama,
            OllamaModel = "qwen3.5:4b",
            OllamaBaseUri = new Uri("http://127.0.0.1:11434/"),
        };
}
