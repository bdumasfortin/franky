using System.Text;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace Franky.Runtime.Speech;

public sealed class WhisperNetSpeechTranscriber : ISpeechTranscriber, IDisposable
{
    private const string ModelDisplayName = "small.en";
    private const string ModelFileName = "ggml-small.en.bin";

    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private readonly SemaphoreSlim transcriptionGate = new(1, 1);
    private readonly string modelPath;
    private WhisperFactory? factory;
    private SpeechTranscriberStatus status = new(
        "preparing",
        "Checking the local speech model",
        ModelDisplayName,
        IsReady: false);

    public WhisperNetSpeechTranscriber()
    {
        var modelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Franky",
            "models");
        modelPath = Path.Combine(modelDirectory, ModelFileName);
    }

    public SpeechTranscriberStatus Status => status;

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (factory is not null) return;

        await initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (factory is not null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            if (!File.Exists(modelPath))
            {
                status = new(
                    "downloading",
                    "Downloading the local speech model once",
                    ModelDisplayName,
                    IsReady: false);
                await DownloadModelAsync(cancellationToken);
            }

            status = new(
                "loading",
                "Loading the local speech model",
                ModelDisplayName,
                IsReady: false);
            factory = WhisperFactory.FromPath(modelPath);
            var runtimeDetail = RuntimeOptions.LoadedLibrary == RuntimeLibrary.Cuda
                ? "Local transcription is ready · NVIDIA GPU"
                : "Local transcription is ready · CPU fallback";
            status = new(
                "ready",
                runtimeDetail,
                ModelDisplayName,
                IsReady: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            status = new(
                "error",
                "The local speech model could not be prepared",
                ModelDisplayName,
                IsReady: false);
            throw;
        }
        finally
        {
            initializationGate.Release();
        }
    }

    public async Task<SpeechTranscript> TranscribeAsync(
        Stream pcmWave,
        CancellationToken cancellationToken)
    {
        PcmWaveValidator.ValidateMono16KhzPcm(pcmWave);
        await PrepareAsync(cancellationToken);
        await transcriptionGate.WaitAsync(cancellationToken);
        try
        {
            pcmWave.Position = 0;
            using var processor = factory!.CreateBuilder()
                .WithLanguage("en")
                .Build();
            var text = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(pcmWave).WithCancellation(cancellationToken))
            {
                var part = segment.Text.Trim();
                if (part.Length == 0 || IsNonSpeechAnnotation(part)) continue;
                if (text.Length > 0) text.Append(' ');
                text.Append(part);
            }

            return new SpeechTranscript(text.ToString(), ModelDisplayName);
        }
        finally
        {
            transcriptionGate.Release();
        }
    }

    private static bool IsNonSpeechAnnotation(string text) => text.ToUpperInvariant() switch
    {
        "[BLANK_AUDIO]" or "[SILENCE]" or "[NO SPEECH]" => true,
        _ => false,
    };

    public void Dispose()
    {
        factory?.Dispose();
        initializationGate.Dispose();
        transcriptionGate.Dispose();
    }

    private async Task DownloadModelAsync(CancellationToken cancellationToken)
    {
        var temporaryPath = modelPath + ".download";
        try
        {
            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(GgmlType.SmallEn);
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await modelStream.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, modelPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
