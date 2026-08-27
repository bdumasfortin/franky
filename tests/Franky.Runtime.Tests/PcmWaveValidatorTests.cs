using Franky.Runtime.Speech;

namespace Franky.Runtime.Tests;

internal static class PcmWaveValidatorTests
{
    public static Task AcceptsFrankyMonoPcm()
    {
        using var wave = CreateWave(channels: 1, sampleRate: 16000, bitsPerSample: 16, sampleBytes: 640);
        PcmWaveValidator.ValidateMono16KhzPcm(wave);
        TestAssert.Equal(0L, wave.Position);
        return Task.CompletedTask;
    }

    public static Task RejectsStereoWakeAudio()
    {
        using var wave = CreateWave(channels: 2, sampleRate: 16000, bitsPerSample: 16, sampleBytes: 1280);
        try
        {
            PcmWaveValidator.ValidateMono16KhzPcm(wave);
        }
        catch (InvalidDataException exception)
        {
            TestAssert.Contains("16 kHz, 16-bit, mono", exception.Message);
            TestAssert.Equal(0L, wave.Position);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("Expected stereo audio to be rejected.");
    }

    private static MemoryStream CreateWave(
        ushort channels,
        uint sampleRate,
        ushort bitsPerSample,
        int sampleBytes)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((uint)(36 + sampleBytes));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            var bytesPerSample = bitsPerSample / 8;
            writer.Write(sampleRate * channels * (uint)bytesPerSample);
            writer.Write((ushort)(channels * bytesPerSample));
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write((uint)sampleBytes);
            writer.Write(new byte[sampleBytes]);
        }
        stream.Position = 0;
        return stream;
    }
}
