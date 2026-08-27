using System.Text;

namespace Franky.Runtime.Speech;

public static class PcmWaveValidator
{
    public static void ValidateMono16KhzPcm(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidDataException("The audio stream must be readable and seekable.");
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (ReadFourCc(reader) != "RIFF") throw InvalidWave();
            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE") throw InvalidWave();

            ushort? format = null;
            ushort? channels = null;
            uint? sampleRate = null;
            ushort? bitsPerSample = null;
            uint? dataLength = null;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = ReadFourCc(reader);
                var chunkLength = reader.ReadUInt32();
                var nextChunk = checked(stream.Position + chunkLength + (chunkLength & 1));
                if (nextChunk > stream.Length) throw InvalidWave();

                if (chunkId == "fmt ")
                {
                    if (chunkLength < 16) throw InvalidWave();
                    format = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadUInt32();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                }
                else if (chunkId == "data")
                {
                    dataLength = chunkLength;
                }

                stream.Position = nextChunk;
            }

            if (format != 1 || channels != 1 || sampleRate != 16000 ||
                bitsPerSample != 16 || dataLength is null or 0)
            {
                throw new InvalidDataException(
                    "Franky expects non-empty 16 kHz, 16-bit, mono PCM WAV audio.");
            }
        }
        catch (EndOfStreamException)
        {
            throw InvalidWave();
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static string ReadFourCc(BinaryReader reader) =>
        Encoding.ASCII.GetString(reader.ReadBytes(4));

    private static InvalidDataException InvalidWave() =>
        new("The request did not contain a valid PCM WAV file.");
}
