namespace Franky.Runtime.Diagnostics;

public interface IEventSink
{
    void Write(string eventName, IReadOnlyDictionary<string, object?> properties);
}
