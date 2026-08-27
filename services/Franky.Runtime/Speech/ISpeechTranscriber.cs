namespace Franky.Runtime.Speech;

public interface ISpeechTranscriber
{
    SpeechTranscriberStatus Status { get; }

    Task PrepareAsync(CancellationToken cancellationToken);

    Task<SpeechTranscript> TranscribeAsync(
        Stream pcmWave,
        CancellationToken cancellationToken);
}

public sealed record SpeechTranscriberStatus(
    string State,
    string Detail,
    string Model,
    bool IsReady,
    bool IsLocal = true);

public sealed record SpeechTranscript(string Text, string Model);
