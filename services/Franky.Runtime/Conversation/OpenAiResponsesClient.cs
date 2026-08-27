using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Franky.Runtime.Configuration;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

namespace Franky.Runtime.Conversation;

public sealed class OpenAiResponsesClient(
    HttpClient httpClient,
    AssistantOptions options,
    IToolExecutor toolExecutor,
    IEventSink events) : IConversationClient
{
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

    public string ProviderName => $"OpenAI Responses API ({options.OpenAiModel})";

    public async Task<AssistantReply> SendAsync(
        ConversationSession session,
        string userText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);

        object input = userText;
        var previousResponseId = session.PreviousResponseId;
        var totalToolCalls = 0;
        var actions = new List<AssistantActionOutcome>();

        for (var round = 0; round <= options.MaxToolRounds; round++)
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = options.OpenAiModel,
                ["instructions"] = Instructions,
                ["input"] = input,
                ["tools"] = toolExecutor.ToolDefinitions.Select(ToOpenAiToolDefinition).ToArray(),
                ["tool_choice"] = "auto",
                ["store"] = true,
            };

            if (!string.IsNullOrWhiteSpace(previousResponseId))
            {
                payload["previous_response_id"] = previousResponseId;
            }

            var started = Stopwatch.GetTimestamp();
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses")
            {
                Content = JsonContent.Create(payload, options: SerializerOptions),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.OpenAiApiKey ?? throw new InvalidOperationException("OPENAI_API_KEY is required for the OpenAI provider."));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            events.Write("openai.response", new Dictionary<string, object?>
            {
                ["success"] = response.IsSuccessStatusCode,
                ["status_code"] = (int)response.StatusCode,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });

            if (!response.IsSuccessStatusCode)
            {
                throw new OpenAiRequestException((int)response.StatusCode, RedactError(body));
            }

            var parsed = OpenAiResponseParser.Parse(body);
            session.PreviousResponseId = parsed.ResponseId;

            if (parsed.ToolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(parsed.OutputText))
                {
                    throw new InvalidOperationException("OpenAI returned neither text nor a supported tool call.");
                }

                return new AssistantReply(parsed.OutputText, totalToolCalls, actions);
            }

            if (round == options.MaxToolRounds)
            {
                throw new InvalidOperationException("The assistant exceeded the configured tool-call round limit.");
            }

            var toolOutputs = new List<object>();
            foreach (var toolCall in parsed.ToolCalls)
            {
                var result = await toolExecutor.ExecuteAsync(
                    new ToolCall(toolCall.Name, toolCall.ArgumentsJson),
                    cancellationToken);
                totalToolCalls++;
                actions.Add(new AssistantActionOutcome(
                    result.ActionName ?? toolCall.Name,
                    result.Success));
                toolOutputs.Add(new Dictionary<string, object?>
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = toolCall.CallId,
                    ["output"] = result.OutputJson,
                });
            }

            input = toolOutputs;
            previousResponseId = parsed.ResponseId;
        }

        throw new UnreachableException();
    }

    private static object ToOpenAiToolDefinition(ToolDefinition tool) =>
        new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["strict"] = tool.Strict,
            ["parameters"] = tool.Parameters,
        };

    private static string RedactError(string responseBody)
    {
        const int limit = 1_000;
        var compact = responseBody.ReplaceLineEndings(" ");
        return compact.Length <= limit ? compact : compact[..limit] + "…";
    }
}

public sealed class OpenAiRequestException(int statusCode, string responseBody)
    : Exception($"OpenAI request failed with HTTP {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
}
