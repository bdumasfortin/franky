using System.Text;
using Franky.Runtime.ControlBoard;

namespace Franky.Runtime.Tests;

internal static class WakeDatasetStoreTests
{
    public static async Task SavesListsAndDeletesLocalSamples()
    {
        var root = Path.Combine(Path.GetTempPath(), $"franky-wake-dataset-{Guid.NewGuid():N}");
        try
        {
            var store = new WakeDatasetStore(root);
            await using var wave = CreateWave(sampleBytes: 96_000);
            var sample = await store.SaveAsync(
                "positive",
                wave,
                new WakeDatasetSampleDetails(
                    "positive-01",
                    "Yo Franky",
                    "20 inches",
                    "facing",
                    30),
                CancellationToken.None);

            TestAssert.Equal("positive", sample.Category);
            TestAssert.Equal(3_000, sample.DurationMilliseconds);
            TestAssert.Equal("corpus", sample.Purpose);
            TestAssert.True(File.Exists(store.GetAudioPath(sample.Id)));

            await using var parityWave = CreateWave(sampleBytes: 96_000);
            var paritySample = await store.SaveAsync(
                "positive",
                parityWave,
                new WakeDatasetSampleDetails(
                    "parity-positive",
                    "Yo Franky",
                    "20 inches",
                    "facing",
                    30,
                    "parity",
                    88),
                CancellationToken.None);
            TestAssert.Equal("parity", paritySample.Purpose);
            TestAssert.Equal(88, paritySample.BoardPeakScorePercent);

            var status = await store.GetStatusAsync(CancellationToken.None);
            TestAssert.Equal(1, status.PositiveCount);
            TestAssert.Equal(0, status.HardNegativeCount);
            TestAssert.Equal(2, status.Samples.Count);

            TestAssert.True(await store.DeleteAsync(sample.Id, CancellationToken.None));
            TestAssert.False(await store.DeleteAsync(sample.Id, CancellationToken.None));
            TestAssert.False(await store.DeleteAsync("../outside", CancellationToken.None));
            status = await store.GetStatusAsync(CancellationToken.None);
            TestAssert.Equal(0, status.PositiveCount);
            TestAssert.Equal(paritySample.Id, status.Samples.Single().Id);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static async Task RejectsInvalidCategoryAndStereoAudio()
    {
        var root = Path.Combine(Path.GetTempPath(), $"franky-wake-dataset-{Guid.NewGuid():N}");
        try
        {
            var store = new WakeDatasetStore(root);
            await using var mono = CreateWave(sampleBytes: 96_000);
            await ExpectInvalidAsync(() => store.SaveAsync(
                "unknown",
                mono,
                new WakeDatasetSampleDetails(null, null, null, null, 30),
                CancellationToken.None));

            await using var stereo = CreateWave(sampleBytes: 192_000, channels: 2);
            await ExpectInvalidAsync(() => store.SaveAsync(
                "positive",
                stereo,
                new WakeDatasetSampleDetails(null, null, null, null, 30),
                CancellationToken.None));

            await using var invalidScore = CreateWave(sampleBytes: 96_000);
            await ExpectInvalidAsync(() => store.SaveAsync(
                "positive",
                invalidScore,
                new WakeDatasetSampleDetails(null, null, null, null, 30, "parity", 101),
                CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task ExpectInvalidAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException("Expected invalid wake-dataset input to be rejected.");
    }

    private static MemoryStream CreateWave(int sampleBytes, ushort channels = 1)
    {
        const uint sampleRate = 16_000;
        const ushort bitsPerSample = 16;
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((uint)(36 + sampleBytes));
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            var bytesPerSample = bitsPerSample / 8;
            writer.Write(sampleRate * channels * (uint)bytesPerSample);
            writer.Write((ushort)(channels * bytesPerSample));
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((uint)sampleBytes);
            writer.Write(new byte[sampleBytes]);
        }
        stream.Position = 0;
        return stream;
    }
}
