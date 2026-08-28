using Franky.Runtime.Diagnostics;
using Franky.Runtime.Speech;

namespace Franky.Runtime.Tests;

internal static class SpeechSynthesisCoordinatorTests
{
    public static async Task AcceptsBoundedFrankyPcm()
    {
        var events = new RecordingEventSink();
        var coordinator = new SpeechSynthesisCoordinator(
            new FixedSpeechSynthesizer(new byte[] { 0, 0, 1, 0 }),
            events);

        var speech = await coordinator.SynthesizeAsync(
            "Hello Bryan.",
            "test-voice",
            CancellationToken.None);

        TestAssert.Equal(4, speech.Audio.Length);
        TestAssert.Equal(SpeechAudioFormat.FrankyPcm, speech.Format);
        TestAssert.Equal("speech.synthesized", events.Entries.Single().Name);
        TestAssert.Equal(true, events.Entries.Single().Properties["success"]);
        TestAssert.False(events.Entries.Single().Properties.ContainsKey("text"));
    }

    public static async Task RejectsUnsupportedAudioFormat()
    {
        var unsupportedFormat = new SpeechAudioFormat("pcm_s16le", 24_000, 1, 16);
        var coordinator = new SpeechSynthesisCoordinator(
            new FixedSpeechSynthesizer(new byte[] { 0, 0 }, unsupportedFormat),
            new RecordingEventSink());

        try
        {
            await coordinator.SynthesizeAsync("Hello.", null, CancellationToken.None);
            throw new InvalidOperationException("Unsupported speech audio was accepted.");
        }
        catch (InvalidDataException)
        {
        }
    }

    public static async Task RejectsOversizedOrPartialAudio()
    {
        var oversized = new SpeechSynthesisCoordinator(
            new FixedSpeechSynthesizer(
                new byte[SpeechSynthesisCoordinator.MaxAudioBytes + 2]),
            new RecordingEventSink());
        var partialSample = new SpeechSynthesisCoordinator(
            new FixedSpeechSynthesizer(new byte[] { 0 }),
            new RecordingEventSink());

        await ExpectInvalidAudioAsync(oversized);
        await ExpectInvalidAudioAsync(partialSample);
    }

    public static async Task RejectsOverlappingSynthesis()
    {
        var synthesizer = new BlockingSpeechSynthesizer();
        var coordinator = new SpeechSynthesisCoordinator(
            synthesizer,
            new RecordingEventSink());
        var firstSynthesis = coordinator.SynthesizeAsync(
            "First.",
            null,
            CancellationToken.None);
        await synthesizer.Started.Task;

        try
        {
            await coordinator.SynthesizeAsync("Second.", null, CancellationToken.None);
            throw new InvalidOperationException("Overlapping synthesis was accepted.");
        }
        catch (SpeechSynthesisBusyException)
        {
        }
        finally
        {
            synthesizer.Release.SetResult();
            await firstSynthesis;
        }
    }

    public static async Task CancelsActiveSynthesis()
    {
        var synthesizer = new BlockingSpeechSynthesizer();
        var events = new RecordingEventSink();
        var coordinator = new SpeechSynthesisCoordinator(synthesizer, events);
        var synthesis = coordinator.SynthesizeAsync(
            "Please stop.",
            null,
            CancellationToken.None);
        await synthesizer.Started.Task;

        TestAssert.True(coordinator.CancelCurrentSynthesis());
        try
        {
            await synthesis;
            throw new InvalidOperationException("Cancelled synthesis completed successfully.");
        }
        catch (OperationCanceledException)
        {
        }

        TestAssert.False(coordinator.CancelCurrentSynthesis());
        TestAssert.Equal(true, events.Entries.Single().Properties["cancelled"]);
    }

    private static async Task ExpectInvalidAudioAsync(
        SpeechSynthesisCoordinator coordinator)
    {
        try
        {
            await coordinator.SynthesizeAsync("Hello.", null, CancellationToken.None);
            throw new InvalidOperationException("Invalid speech audio was accepted.");
        }
        catch (InvalidDataException)
        {
        }
    }

    private sealed class FixedSpeechSynthesizer(
        byte[] audio,
        SpeechAudioFormat? format = null) : ISpeechSynthesizer
    {
        public SpeechSynthesizerStatus Status { get; } = new(
            "ready",
            "Test synthesizer ready.",
            "test",
            IsReady: true);

        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SynthesizedSpeech> SynthesizeAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SynthesizedSpeech(
                audio,
                format ?? SpeechAudioFormat.FrankyPcm,
                request.VoiceId ?? "test-default"));
    }

    private sealed class BlockingSpeechSynthesizer : ISpeechSynthesizer
    {
        public SpeechSynthesizerStatus Status { get; } = new(
            "ready",
            "Test synthesizer ready.",
            "test",
            IsReady: true);
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<SynthesizedSpeech> SynthesizeAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new SynthesizedSpeech(
                new byte[] { 0, 0 },
                SpeechAudioFormat.FrankyPcm,
                "test-default");
        }
    }

    private sealed class RecordingEventSink : IEventSink
    {
        public List<(string Name, IReadOnlyDictionary<string, object?> Properties)> Entries { get; } = [];

        public void Write(
            string eventName,
            IReadOnlyDictionary<string, object?> properties)
        {
            Entries.Add((eventName, properties));
        }
    }
}
