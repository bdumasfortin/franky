using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Franky.Runtime.ControlBoard;

public sealed class WakeDatasetStore
{
    public const int MaxAudioBytes = 256 * 1024;
    public const int PositiveTarget = 30;
    public const int HardNegativeTarget = 20;
    public const int MaximumSamples = 100;

    private static readonly string[] Categories = ["positive", "hard-negative"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WakeDatasetStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    public string Root => _root;

    public async Task<WakeDatasetSample> SaveAsync(
        string category,
        Stream wave,
        WakeDatasetSampleDetails details,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        ArgumentNullException.ThrowIfNull(wave);
        ArgumentNullException.ThrowIfNull(details);
        var normalizedPurpose = NormalizePurpose(details.Purpose);
        var boardPeakScorePercent = NormalizeBoardPeakScore(details.BoardPeakScorePercent);

        await using var audio = new MemoryStream();
        var transferBuffer = new byte[16 * 1024];
        while (true)
        {
            var read = await wave.ReadAsync(transferBuffer, cancellationToken);
            if (read == 0) break;
            if (audio.Length + read > MaxAudioBytes)
            {
                throw new InvalidDataException("The wake sample is empty or too large.");
            }
            await audio.WriteAsync(transferBuffer.AsMemory(0, read), cancellationToken);
        }
        if (audio.Length is <= 0 or > MaxAudioBytes)
        {
            throw new InvalidDataException("The wake sample is empty or too large.");
        }

        audio.Position = 0;
        var audioMetrics = ValidateCanonicalWave(audio);
        var durationMilliseconds = audioMetrics.DurationMilliseconds;
        if (durationMilliseconds is < 500 or > 5_000)
        {
            throw new InvalidDataException("Wake samples must be between 0.5 and 5 seconds.");
        }

        var id = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        var sample = new WakeDatasetSample(
            id,
            normalizedCategory,
            DateTimeOffset.UtcNow,
            durationMilliseconds,
            checked((int)audio.Length),
            Clean(details.PromptId, 60),
            Clean(details.Prompt, 160),
            Clean(details.Distance, 40),
            Clean(details.Orientation, 40),
            Math.Clamp(details.GainDb, 0, 30),
            "afe_processed_mono_v1",
            Convert.ToHexString(SHA256.HashData(audio.ToArray())).ToLowerInvariant(),
            audioMetrics.PeakDbfs,
            audioMetrics.RmsDbfs,
            normalizedPurpose,
            boardPeakScorePercent);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (ReadSamples().Count >= MaximumSamples)
            {
                throw new InvalidOperationException(
                    $"The local wake dataset is limited to {MaximumSamples} samples.");
            }
            var directory = CategoryDirectory(normalizedCategory);
            Directory.CreateDirectory(directory);
            var wavePath = Path.Combine(directory, $"{id}.wav");
            var metadataPath = Path.Combine(directory, $"{id}.json");
            var waveTemporaryPath = $"{wavePath}.tmp";
            var metadataTemporaryPath = $"{metadataPath}.tmp";

            try
            {
                audio.Position = 0;
                await using (var output = new FileStream(
                    waveTemporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    16 * 1024,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await audio.CopyToAsync(output, cancellationToken);
                }

                await File.WriteAllTextAsync(
                    metadataTemporaryPath,
                    JsonSerializer.Serialize(sample, JsonOptions),
                    Encoding.UTF8,
                    cancellationToken);
                File.Move(waveTemporaryPath, wavePath);
                File.Move(metadataTemporaryPath, metadataPath);
            }
            catch
            {
                DeleteIfPresent(waveTemporaryPath);
                DeleteIfPresent(metadataTemporaryPath);
                DeleteIfPresent(wavePath);
                DeleteIfPresent(metadataPath);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }

        return sample;
    }

    public async Task<WakeDatasetStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var samples = ReadSamples();
            return new WakeDatasetStatus(
                "tools/wake-word/.cache/recordings",
                PositiveTarget,
                HardNegativeTarget,
                samples.Count(sample => sample.Category == "positive" && IsCorpusSample(sample)),
                samples.Count(sample => sample.Category == "hard-negative" && IsCorpusSample(sample)),
                samples);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (!IsValidId(id)) return false;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var category in Categories)
            {
                var directory = CategoryDirectory(category);
                var metadataPath = Path.Combine(directory, $"{id}.json");
                var wavePath = Path.Combine(directory, $"{id}.wav");
                if (!File.Exists(metadataPath) && !File.Exists(wavePath)) continue;

                DeleteIfPresent(metadataPath);
                DeleteIfPresent(wavePath);
                return true;
            }
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var deletedSamples = 0;
            foreach (var category in Categories)
            {
                var directory = CategoryDirectory(category);
                if (!Directory.Exists(directory)) continue;

                foreach (var metadataPath in Directory.EnumerateFiles(directory, "*.json"))
                {
                    var id = Path.GetFileNameWithoutExtension(metadataPath);
                    if (!IsValidId(id)) continue;
                    DeleteIfPresent(metadataPath);
                    DeleteIfPresent(Path.Combine(directory, $"{id}.wav"));
                    deletedSamples += 1;
                }

                foreach (var temporaryPath in Directory.EnumerateFiles(directory, "*.tmp"))
                {
                    DeleteIfPresent(temporaryPath);
                }
            }
            return deletedSamples;
        }
        finally
        {
            _gate.Release();
        }
    }

    public string? GetAudioPath(string id)
    {
        if (!IsValidId(id)) return null;
        foreach (var category in Categories)
        {
            var path = Path.Combine(CategoryDirectory(category), $"{id}.wav");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private IReadOnlyList<WakeDatasetSample> ReadSamples()
    {
        var samples = new List<WakeDatasetSample>();
        foreach (var category in Categories)
        {
            var directory = CategoryDirectory(category);
            if (!Directory.Exists(directory)) continue;

            foreach (var metadataPath in Directory.EnumerateFiles(directory, "*.json"))
            {
                try
                {
                    var sample = JsonSerializer.Deserialize<WakeDatasetSample>(
                        File.ReadAllText(metadataPath),
                        JsonOptions);
                    if (sample is not null && IsValidId(sample.Id) && sample.Category == category &&
                        File.Exists(Path.Combine(directory, $"{sample.Id}.wav")))
                    {
                        samples.Add(sample);
                    }
                }
                catch (JsonException)
                {
                    // A damaged sidecar is ignored instead of exposing an unverified file.
                }
            }
        }

        return samples
            .OrderByDescending(sample => sample.CreatedAtUtc)
            .ToArray();
    }

    private string CategoryDirectory(string category) => Path.Combine(_root, category);

    private static string NormalizeCategory(string category)
    {
        var normalized = category?.Trim().ToLowerInvariant();
        if (!Categories.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException("Category must be positive or hard-negative.", nameof(category));
        }
        return normalized!;
    }

    private static string Clean(string? value, int maximumLength)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    private static string NormalizePurpose(string? purpose)
    {
        var normalized = string.IsNullOrWhiteSpace(purpose)
            ? "corpus"
            : purpose.Trim().ToLowerInvariant();
        if (normalized is not ("corpus" or "parity"))
        {
            throw new ArgumentException("Purpose must be corpus or parity.", nameof(purpose));
        }
        return normalized;
    }

    private static int? NormalizeBoardPeakScore(int? score)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                "Board peak score must be between 0 and 100 percent.");
        }
        return score;
    }

    private static bool IsCorpusSample(WakeDatasetSample sample) =>
        !string.Equals(sample.Purpose, "parity", StringComparison.Ordinal);

    private static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Length <= 80 &&
        id.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static WakeDatasetAudioMetrics ValidateCanonicalWave(Stream stream)
    {
        var originalPosition = stream.Position;
        try
        {
            if (stream.Length < 44) throw InvalidCanonicalWave();
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF" ||
                reader.ReadUInt32() != stream.Length - 8 ||
                Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE" ||
                Encoding.ASCII.GetString(reader.ReadBytes(4)) != "fmt " ||
                reader.ReadUInt32() != 16 ||
                reader.ReadUInt16() != 1 ||
                reader.ReadUInt16() != 1 ||
                reader.ReadUInt32() != 16_000 ||
                reader.ReadUInt32() != 32_000 ||
                reader.ReadUInt16() != 2 ||
                reader.ReadUInt16() != 16 ||
                Encoding.ASCII.GetString(reader.ReadBytes(4)) != "data")
            {
                throw InvalidCanonicalWave();
            }

            var dataBytes = reader.ReadUInt32();
            if (dataBytes == 0 || (dataBytes & 1) != 0 || dataBytes != stream.Length - 44)
            {
                throw InvalidCanonicalWave();
            }

            var peak = 0d;
            var sumSquares = 0d;
            var sampleCount = dataBytes / 2;
            for (var index = 0u; index < sampleCount; index += 1)
            {
                var normalized = reader.ReadInt16() / 32768d;
                peak = Math.Max(peak, Math.Abs(normalized));
                sumSquares += normalized * normalized;
            }

            var peakDbfs = peak == 0 ? -120d : 20 * Math.Log10(peak);
            var rms = Math.Sqrt(sumSquares / sampleCount);
            var rmsDbfs = rms == 0 ? -120d : 20 * Math.Log10(rms);
            return new WakeDatasetAudioMetrics(
                checked((int)Math.Round(dataBytes * 1000d / 32_000)),
                Math.Round(peakDbfs, 2),
                Math.Round(rmsDbfs, 2));
        }
        catch (EndOfStreamException)
        {
            throw InvalidCanonicalWave();
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static InvalidDataException InvalidCanonicalWave() =>
        new("Franky expects a canonical 16 kHz, 16-bit, mono PCM WAV sample.");

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

public sealed record WakeDatasetSampleDetails(
    string? PromptId,
    string? Prompt,
    string? Distance,
    string? Orientation,
    int GainDb,
    string? Purpose = null,
    int? BoardPeakScorePercent = null);

public sealed record WakeDatasetSample(
    string Id,
    string Category,
    DateTimeOffset CreatedAtUtc,
    int DurationMilliseconds,
    int AudioBytes,
    string PromptId,
    string Prompt,
    string Distance,
    string Orientation,
    int GainDb,
    string CapturePipeline,
    string Sha256,
    double PeakDbfs,
    double RmsDbfs,
    string? Purpose = null,
    int? BoardPeakScorePercent = null);

public sealed record WakeDatasetStatus(
    string Storage,
    int PositiveTarget,
    int HardNegativeTarget,
    int PositiveCount,
    int HardNegativeCount,
    IReadOnlyList<WakeDatasetSample> Samples);

internal sealed record WakeDatasetAudioMetrics(
    int DurationMilliseconds,
    double PeakDbfs,
    double RmsDbfs);
