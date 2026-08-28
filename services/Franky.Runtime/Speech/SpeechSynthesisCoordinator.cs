using System.Diagnostics;
using Franky.Runtime.Diagnostics;

namespace Franky.Runtime.Speech;

public sealed class SpeechSynthesisCoordinator(
    ISpeechSynthesizer synthesizer,
    IEventSink events)
{
    public const int MaxTextCharacters = 4_000;
    public const int MaxAudioDurationSeconds = 30;
    public const int MaxAudioBytes =
        SpeechSampleRateHz * SpeechChannels * (SpeechBitsPerSample / 8) * MaxAudioDurationSeconds;

    private const int SpeechSampleRateHz = 16_000;
    private const int SpeechChannels = 1;
    private const int SpeechBitsPerSample = 16;

    private readonly SemaphoreSlim synthesisGate = new(1, 1);
    private readonly object cancellationLock = new();
    private CancellationTokenSource? activeSynthesis;

    public string ProviderName => synthesizer.Status.Provider;
    public SpeechSynthesizerStatus Status => synthesizer.Status;

    public Task PrepareAsync(CancellationToken cancellationToken) =>
        synthesizer.PrepareAsync(cancellationToken);

    public async Task<SynthesizedSpeech> SynthesizeAsync(
        string text,
        string? voiceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > MaxTextCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"Speech text cannot exceed {MaxTextCharacters} characters.");
        }
        if (!await synthesisGate.WaitAsync(0, cancellationToken))
        {
            throw new SpeechSynthesisBusyException();
        }

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (cancellationLock)
        {
            activeSynthesis = linkedCancellation;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var speech = await synthesizer.SynthesizeAsync(
                new SpeechSynthesisRequest(text, voiceId),
                linkedCancellation.Token);
            Validate(speech);

            events.Write("speech.synthesized", new Dictionary<string, object?>
            {
                ["success"] = true,
                ["provider"] = ProviderName,
                ["voice"] = speech.VoiceId,
                ["text_length"] = text.Length,
                ["audio_bytes"] = speech.Audio.Length,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });
            return speech;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            events.Write("speech.synthesized", new Dictionary<string, object?>
            {
                ["success"] = false,
                ["cancelled"] = true,
                ["provider"] = ProviderName,
                ["text_length"] = text.Length,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });
            throw;
        }
        catch (Exception exception)
        {
            events.Write("speech.synthesized", new Dictionary<string, object?>
            {
                ["success"] = false,
                ["provider"] = ProviderName,
                ["error_type"] = exception.GetType().Name,
                ["text_length"] = text.Length,
                ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            });
            throw;
        }
        finally
        {
            lock (cancellationLock)
            {
                if (ReferenceEquals(activeSynthesis, linkedCancellation))
                {
                    activeSynthesis = null;
                }
            }
            synthesisGate.Release();
        }
    }

    public bool CancelCurrentSynthesis()
    {
        lock (cancellationLock)
        {
            if (activeSynthesis is null)
            {
                return false;
            }

            activeSynthesis.Cancel();
            return true;
        }
    }

    private static void Validate(SynthesizedSpeech speech)
    {
        ArgumentNullException.ThrowIfNull(speech);
        if (speech.Format != SpeechAudioFormat.FrankyPcm)
        {
            throw new InvalidDataException(
                "Franky speech output must be 16 kHz, 16-bit, mono PCM.");
        }
        if (speech.Audio.IsEmpty)
        {
            throw new InvalidDataException("Franky speech output cannot be empty.");
        }
        if (speech.Audio.Length > MaxAudioBytes)
        {
            throw new InvalidDataException(
                $"Franky speech output cannot exceed {MaxAudioDurationSeconds} seconds.");
        }
        if ((speech.Audio.Length % (SpeechBitsPerSample / 8)) != 0)
        {
            throw new InvalidDataException("Franky speech output contains a partial PCM sample.");
        }
        if (string.IsNullOrWhiteSpace(speech.VoiceId))
        {
            throw new InvalidDataException("Franky speech output must identify its voice.");
        }
    }
}

public sealed class SpeechSynthesisBusyException()
    : Exception("Franky is already synthesizing another response.");
