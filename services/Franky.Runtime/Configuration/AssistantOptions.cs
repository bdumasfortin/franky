namespace Franky.Runtime.Configuration;

public sealed record AssistantOptions(
    string? OpenAiApiKey,
    string OpenAiModel,
    Uri OpenAiBaseUri,
    int MaxToolRounds)
{
    public const string DefaultModel = "gpt-5.6-luna";
    public const string DefaultOllamaModel = "qwen3.5:4b";

    public AssistantProvider Provider { get; init; } = AssistantProvider.OpenAi;
    public string OllamaModel { get; init; } = DefaultOllamaModel;
    public Uri OllamaBaseUri { get; init; } = new("http://127.0.0.1:11434/");
    public string OllamaKeepAlive { get; init; } = "1h";

    public bool UseDemoProvider => Provider == AssistantProvider.Demo;
    public bool IsLocal => Provider is AssistantProvider.Demo or AssistantProvider.Ollama;

    public static AssistantOptions FromEnvironment(IEnumerable<string> arguments)
    {
        var forceDemo = arguments.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var provider = forceDemo
            ? AssistantProvider.Demo
            : ParseProvider(Environment.GetEnvironmentVariable("FRANKY_ASSISTANT_PROVIDER"), apiKey);
        var model = Environment.GetEnvironmentVariable("FRANKY_OPENAI_MODEL") ??
            Environment.GetEnvironmentVariable("ASSISTANT_OPENAI_MODEL");
        var baseUrl = Environment.GetEnvironmentVariable("FRANKY_OPENAI_BASE_URL") ??
            Environment.GetEnvironmentVariable("ASSISTANT_OPENAI_BASE_URL");
        var ollamaModel = Environment.GetEnvironmentVariable("FRANKY_OLLAMA_MODEL");
        var ollamaBaseUrl = Environment.GetEnvironmentVariable("FRANKY_OLLAMA_BASE_URL");
        var ollamaKeepAlive = Environment.GetEnvironmentVariable("FRANKY_OLLAMA_KEEP_ALIVE");

        if (!Uri.TryCreate(baseUrl ?? "https://api.openai.com/", UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("FRANKY_OPENAI_BASE_URL must be an absolute URI.");
        }

        if (!Uri.TryCreate(ollamaBaseUrl ?? "http://127.0.0.1:11434/", UriKind.Absolute, out var ollamaBaseUri))
        {
            throw new InvalidOperationException("FRANKY_OLLAMA_BASE_URL must be an absolute URI.");
        }

        return new AssistantOptions(
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            EnsureTrailingSlash(baseUri),
            MaxToolRounds: 4)
        {
            Provider = provider,
            OllamaModel = string.IsNullOrWhiteSpace(ollamaModel) ? DefaultOllamaModel : ollamaModel,
            OllamaBaseUri = EnsureTrailingSlash(ollamaBaseUri),
            OllamaKeepAlive = string.IsNullOrWhiteSpace(ollamaKeepAlive) ? "1h" : ollamaKeepAlive,
        };
    }

    private static AssistantProvider ParseProvider(string? value, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.IsNullOrWhiteSpace(apiKey)
                ? AssistantProvider.Demo
                : AssistantProvider.OpenAi;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "demo" => AssistantProvider.Demo,
            "ollama" => AssistantProvider.Ollama,
            "openai" => AssistantProvider.OpenAi,
            _ => throw new InvalidOperationException(
                "FRANKY_ASSISTANT_PROVIDER must be 'demo', 'ollama', or 'openai'."),
        };
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + '/', UriKind.Absolute);
}

public enum AssistantProvider
{
    Demo,
    Ollama,
    OpenAi,
}
