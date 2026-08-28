namespace Franky.Runtime.Speech;

public interface ISpeechSynthesizer
{
    SpeechSynthesizerStatus Status { get; }

    Task PrepareAsync(CancellationToken cancellationToken);

    Task<SynthesizedSpeech> SynthesizeAsync(
        SpeechSynthesisRequest request,
        CancellationToken cancellationToken);
}

public sealed record SpeechSynthesizerStatus(
    string State,
    string Detail,
    string Provider,
    bool IsReady,
    bool IsLocal = true);

public sealed record SpeechSynthesisRequest(
    string Text,
    string? VoiceId = null);

public sealed record SpeechAudioFormat(
    string Encoding,
    int SampleRateHz,
    int Channels,
    int BitsPerSample)
{
    public static SpeechAudioFormat FrankyPcm { get; } = new(
        "pcm_s16le",
        SampleRateHz: 16_000,
        Channels: 1,
        BitsPerSample: 16);
}

public sealed record SynthesizedSpeech(
    ReadOnlyMemory<byte> Audio,
    SpeechAudioFormat Format,
    string VoiceId);
