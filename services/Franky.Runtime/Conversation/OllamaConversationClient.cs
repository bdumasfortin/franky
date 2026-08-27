using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Franky.Runtime.Configuration;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

namespace Franky.Runtime.Conversation;

public sealed class OllamaConversationClient(
    HttpClient httpClient,
    AssistantOptions options,
    IToolExecutor toolExecutor,
    IEventSink events) : IConversationClient
{
    private readonly SemaphoreSlim prepareGate = new(1, 1);
    private volatile bool isPrepared;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private const string Instructions = """
        You are Franky, a concise, warm, slightly playful personal voice assistant.
        Only claim that a computer action succeeded after a tool result reports success.
        Use run_named_command only when the user explicitly asks for one of its documented read-only actions.
        Never suggest that you have arbitrary shell access. If a requested action is unavailable, say so plainly.
        """;

    public string ProviderName => $"Ollama ({options.OllamaModel})";

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (isPrepared)
        {
            return;
        }

        await prepareGate.WaitAsync(cancellationToken);
        try
        {
            if (isPrepared)
            {
                return;
            }

            var started = Stopwatch.GetTimestamp();
            using var response = await httpClient.PostAsJsonAsync(
                "api/generate",
                new Dictionary<string, object?>
                {
                    ["model"] = options.OllamaModel,
                    ["stream"] = false,
                    ["keep_alive"] = options.OllamaKeepAlive,
                },
                SerializerOptions,
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            events.Write("ollama.prepared", new Dictionary<string, object?>
            {
                ["success"] = response.IsSuccessStatusCode,
                ["status_code"] = (int)response.StatusCode,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });

            if (!response.IsSuccessStatusCode)
            {
                throw new OllamaRequestException((int)response.StatusCode, RedactError(body));
            }

            isPrepared = true;
        }
        finally
        {
            prepareGate.Release();
        }
    }

    public async Task<AssistantReply> SendAsync(
        ConversationSession session,
        string userText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        await PrepareAsync(cancellationToken);

        var messages = session.LocalMessages.ToList();
        if (messages.Count == 0)
        {
            messages.Add(new LocalConversationMessage("system", Instructions));
        }
        messages.Add(new LocalConversationMessage("user", userText));

        var totalToolCalls = 0;
        var actions = new List<AssistantActionOutcome>();

        for (var round = 0; round <= options.MaxToolRounds; round++)
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = options.OllamaModel,
                ["messages"] = messages.Select(ToOllamaMessage).ToArray(),
                ["tools"] = toolExecutor.ToolDefinitions.Select(ToOllamaToolDefinition).ToArray(),
                ["stream"] = false,
                ["think"] = false,
                ["keep_alive"] = options.OllamaKeepAlive,
            };

            var started = Stopwatch.GetTimestamp();
            using var response = await httpClient.PostAsJsonAsync(
                "api/chat",
                payload,
                SerializerOptions,
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            events.Write("ollama.response", new Dictionary<string, object?>
            {
                ["success"] = response.IsSuccessStatusCode,
                ["status_code"] = (int)response.StatusCode,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });

            if (!response.IsSuccessStatusCode)
            {
                throw new OllamaRequestException((int)response.StatusCode, RedactError(body));
            }

            var parsed = ParseResponse(body);
            messages.Add(new LocalConversationMessage(
                "assistant",
                parsed.Text,
                parsed.ToolCalls));

            if (parsed.ToolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(parsed.Text))
                {
                    throw new InvalidOperationException("Ollama returned neither text nor a supported tool call.");
                }

                session.ReplaceLocalMessages(messages);
                return new AssistantReply(parsed.Text, totalToolCalls, actions);
            }

            if (round == options.MaxToolRounds)
            {
                throw new InvalidOperationException("The assistant exceeded the configured tool-call round limit.");
            }

            foreach (var toolCall in parsed.ToolCalls)
            {
                var result = await toolExecutor.ExecuteAsync(
                    new ToolCall(toolCall.Name, toolCall.ArgumentsJson),
                    cancellationToken);
                totalToolCalls++;
                actions.Add(new AssistantActionOutcome(
                    result.ActionName ?? toolCall.Name,
                    result.Success));
                messages.Add(new LocalConversationMessage(
                    "tool",
                    result.OutputJson,
                    ToolName: toolCall.Name));
            }
        }

        throw new UnreachableException();
    }

    private static object ToOllamaMessage(LocalConversationMessage message)
    {
        var payload = new Dictionary<string, object?>
        {
            ["role"] = message.Role,
            ["content"] = message.Content ?? string.Empty,
        };

        if (message.ToolCalls is { Count: > 0 })
        {
            payload["tool_calls"] = message.ToolCalls.Select(toolCall =>
                new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = toolCall.Name,
                        ["arguments"] = JsonSerializer.Deserialize<JsonElement>(toolCall.ArgumentsJson),
                    },
                }).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(message.ToolName))
        {
            payload["tool_name"] = message.ToolName;
        }

        return payload;
    }

    private static object ToOllamaToolDefinition(ToolDefinition tool) =>
        new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = tool.Parameters,
            },
        };

    private static ParsedOllamaMessage ParseResponse(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var message = document.RootElement.GetProperty("message");
        var text = message.TryGetProperty("content", out var content)
            ? content.GetString() ?? string.Empty
            : string.Empty;
        var toolCalls = new List<LocalConversationToolCall>();

        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var name = function.GetProperty("name").GetString()
                    ?? throw new InvalidOperationException("Ollama tool call did not contain a name.");
                var arguments = function.TryGetProperty("arguments", out var argumentsElement)
                    ? argumentsElement.ValueKind == JsonValueKind.String
                        ? argumentsElement.GetString() ?? "{}"
                        : argumentsElement.GetRawText()
                    : "{}";
                toolCalls.Add(new LocalConversationToolCall(name, arguments));
            }
        }

        return new ParsedOllamaMessage(text, toolCalls);
    }

    private static string RedactError(string responseBody)
    {
        const int limit = 1_000;
        var compact = responseBody.ReplaceLineEndings(" ");
        return compact.Length <= limit ? compact : compact[..limit] + "…";
    }

    private sealed record ParsedOllamaMessage(
        string Text,
        IReadOnlyList<LocalConversationToolCall> ToolCalls);
}

public sealed class OllamaRequestException(int statusCode, string responseBody)
    : Exception($"Ollama request failed with HTTP {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
}
