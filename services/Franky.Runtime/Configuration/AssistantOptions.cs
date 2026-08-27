namespace Franky.Runtime.Configuration;

public sealed record AssistantOptions(
    bool UseDemoProvider,
    string? OpenAiApiKey,
    string OpenAiModel,
    Uri OpenAiBaseUri,
    int MaxToolRounds)
{
    public const string DefaultModel = "gpt-5.6-luna";

    public static AssistantOptions FromEnvironment(IEnumerable<string> arguments)
    {
        var forceDemo = arguments.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var useDemo = forceDemo || string.IsNullOrWhiteSpace(apiKey);
        var model = Environment.GetEnvironmentVariable("FRANKY_OPENAI_MODEL") ??
            Environment.GetEnvironmentVariable("ASSISTANT_OPENAI_MODEL");
        var baseUrl = Environment.GetEnvironmentVariable("FRANKY_OPENAI_BASE_URL") ??
            Environment.GetEnvironmentVariable("ASSISTANT_OPENAI_BASE_URL");

        if (!Uri.TryCreate(baseUrl ?? "https://api.openai.com/", UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("FRANKY_OPENAI_BASE_URL must be an absolute URI.");
        }

        return new AssistantOptions(
            useDemo,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            EnsureTrailingSlash(baseUri),
            MaxToolRounds: 4);
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + '/', UriKind.Absolute);
}
