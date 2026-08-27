using System.Text.Json;

namespace Franky.Runtime.Diagnostics;

public sealed class JsonEventSink(TextWriter writer) : IEventSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Write(string eventName, IReadOnlyDictionary<string, object?> properties)
    {
        var entry = new Dictionary<string, object?>(properties)
        {
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["event"] = eventName,
        };
        writer.WriteLine(JsonSerializer.Serialize(entry, SerializerOptions));
    }
}
