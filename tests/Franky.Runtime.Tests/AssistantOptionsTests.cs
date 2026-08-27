using Franky.Runtime.Configuration;

namespace Franky.Runtime.Tests;

internal static class AssistantOptionsTests
{
    public static Task SelectsOllamaWithoutOpenAiKey()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                ["FRANKY_ASSISTANT_PROVIDER"] = "ollama",
                ["OPENAI_API_KEY"] = null,
                ["FRANKY_OLLAMA_MODEL"] = "qwen-test:4b",
            },
            () =>
            {
                var options = AssistantOptions.FromEnvironment([]);
                TestAssert.Equal(AssistantProvider.Ollama, options.Provider);
                TestAssert.Equal("qwen-test:4b", options.OllamaModel);
                TestAssert.False(options.UseDemoProvider);
                TestAssert.True(options.IsLocal);
            });

        return Task.CompletedTask;
    }

    public static Task DefaultsToDemoWithoutProviderOrKey()
    {
        WithEnvironment(
            new Dictionary<string, string?>
            {
                ["FRANKY_ASSISTANT_PROVIDER"] = null,
                ["OPENAI_API_KEY"] = null,
            },
            () =>
            {
                var options = AssistantOptions.FromEnvironment([]);
                TestAssert.Equal(AssistantProvider.Demo, options.Provider);
                TestAssert.True(options.UseDemoProvider);
                TestAssert.True(options.IsLocal);
            });

        return Task.CompletedTask;
    }

    private static void WithEnvironment(
        IReadOnlyDictionary<string, string?> values,
        Action action)
    {
        var original = values.ToDictionary(
            entry => entry.Key,
            entry => Environment.GetEnvironmentVariable(entry.Key),
            StringComparer.Ordinal);
        try
        {
            foreach (var entry in values)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
            action();
        }
        finally
        {
            foreach (var entry in original)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}
