using Franky.Runtime.Configuration;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Tools;

namespace Franky.Runtime.Conversation;

public static class ConversationClientFactory
{
    public static IConversationClient Create(
        AssistantOptions options,
        IToolExecutor toolExecutor,
        IEventSink events)
    {
        if (options.Provider == AssistantProvider.Demo)
        {
            return new DemoConversationClient();
        }

        if (options.Provider == AssistantProvider.OpenAi && string.IsNullOrWhiteSpace(options.OpenAiApiKey))
        {
            throw new InvalidOperationException(
                "OPENAI_API_KEY is required when FRANKY_ASSISTANT_PROVIDER is 'openai'.");
        }

        var httpClient = new HttpClient
        {
            BaseAddress = options.Provider == AssistantProvider.Ollama
                ? options.OllamaBaseUri
                : options.OpenAiBaseUri,
            Timeout = TimeSpan.FromSeconds(90),
        };

        return options.Provider switch
        {
            AssistantProvider.Ollama => new OllamaConversationClient(httpClient, options, toolExecutor, events),
            AssistantProvider.OpenAi => new OpenAiResponsesClient(httpClient, options, toolExecutor, events),
            _ => throw new InvalidOperationException($"Unsupported assistant provider: {options.Provider}"),
        };
    }
}
