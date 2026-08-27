using System.Text.Json;

namespace Franky.Runtime.Conversation;

public static class OpenAiResponseParser
{
    public static ParsedResponse Parse(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var responseId = root.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("OpenAI response did not contain an id.");

        var text = new List<string>();
        var toolCalls = new List<ParsedToolCall>();

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (string.Equals(type, "function_call", StringComparison.Ordinal))
                {
                    toolCalls.Add(new ParsedToolCall(
                        item.GetProperty("call_id").GetString()
                            ?? throw new InvalidOperationException("Function call did not contain call_id."),
                        item.GetProperty("name").GetString()
                            ?? throw new InvalidOperationException("Function call did not contain a name."),
                        item.GetProperty("arguments").GetString() ?? "{}"));
                    continue;
                }

                if (!string.Equals(type, "message", StringComparison.Ordinal) ||
                    !item.TryGetProperty("content", out var content) ||
                    content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("type", out var contentType) &&
                        string.Equals(contentType.GetString(), "output_text", StringComparison.Ordinal) &&
                        contentItem.TryGetProperty("text", out var textElement) &&
                        textElement.GetString() is { Length: > 0 } outputText)
                    {
                        text.Add(outputText);
                    }
                }
            }
        }

        return new ParsedResponse(responseId, string.Join(Environment.NewLine, text), toolCalls);
    }
}

public sealed record ParsedResponse(
    string ResponseId,
    string OutputText,
    IReadOnlyList<ParsedToolCall> ToolCalls);

public sealed record ParsedToolCall(string CallId, string Name, string ArgumentsJson);
