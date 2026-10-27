using System.Buffers.Binary;

namespace Afterform.Api.Uploads;

// Bounded structural checks. This is not a complete decoder or malware scanner.
public static class AudioFileInspector
{
    public static string Inspect(string path, string extension, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var valid = extension switch
            {
                ".mp3" => IsMp3(stream, cancellationToken),
                ".wav" => IsWave(stream, cancellationToken),
                ".flac" => IsFlac(stream, cancellationToken),
                _ => false
            };
            if (valid) return extension switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                _ => "audio/flac"
            };
        }
        catch (EndOfStreamException) { }
        throw new DemoFileValidationException(
            "The file content does not match a supported audio format. Export the original as MP3, PCM/float WAV or FLAC.");
    }

    private static bool IsWave(Stream stream, CancellationToken cancellationToken)
    {
        var header = Read(stream, 12);
        if (!header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            || !header.AsSpan(8, 4).SequenceEqual("WAVE"u8)
            || (long)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)) + 8 != stream.Length)
            return false;

        var hasFormat = false;
        var hasAudio = false;
        ushort blockAlign = 0;
        for (var chunks = 0; chunks < 4096 && stream.Position < stream.Length; chunks++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = Read(stream, 8);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4, 4));
            var next = stream.Position + size + (size & 1);
            if (next > stream.Length) return false;

            if (chunk.AsSpan(0, 4).SequenceEqual("fmt "u8))
            {
                if (hasFormat || size < 16) return false;
                var format = Read(stream, (int)Math.Min(size, 40));
                var tag = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(0, 2));
                var channels = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2, 2));
                var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(4, 4));
                var byteRate = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(8, 4));
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(12, 2));
                var bits = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(14, 2));
                if (tag == 0xfffe)
                {
                    if (size < 40 || BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(16, 2)) < 22)
                        return false;
                    var subFormat = new Guid(format.AsSpan(24, 16));
                    if (subFormat == new Guid("00000001-0000-0010-8000-00aa00389b71")) tag = 1;
                    else if (subFormat == new Guid("00000003-0000-0010-8000-00aa00389b71")) tag = 3;
                    else return false;
                    var validBits = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(18, 2));
                    if (validBits == 0 || validBits > bits) return false;
                }
                if (channels is < 1 or > 32 || sampleRate is < 8000 or > 384000
                    || (tag == 1 ? bits is not (8 or 16 or 24 or 32)
                        : tag != 3 || bits is not (32 or 64))
                    || blockAlign != channels * (bits / 8)
                    || byteRate != sampleRate * blockAlign) return false;
                hasFormat = true;
            }
            else if (chunk.AsSpan(0, 4).SequenceEqual("data"u8))
            {
                if (!hasFormat || size == 0 || size % blockAlign != 0) return false;
                hasAudio = true;
            }
            stream.Position = next;
        }
        return hasFormat && hasAudio && stream.Position == stream.Length;
    }

    private static bool IsFlac(Stream stream, CancellationToken cancellationToken)
    {
        if (!Read(stream, 4).AsSpan().SequenceEqual("fLaC"u8)) return false;
        for (var block = 0; block < 4096; block++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = Read(stream, 4);
            var type = header[0] & 0x7f;
            var last = (header[0] & 0x80) != 0;
            var length = (header[1] << 16) | (header[2] << 8) | header[3];
            var next = stream.Position + length;
            if (type == 127 || next > stream.Length || (block == 0 ? type != 0 : type == 0))
                return false;
            if (block == 0)
            {
                if (length != 34) return false;
                var info = Read(stream, 34);
                var minBlock = BinaryPrimitives.ReadUInt16BigEndian(info.AsSpan(0, 2));
                var maxBlock = BinaryPrimitives.ReadUInt16BigEndian(info.AsSpan(2, 2));
                var packed = BinaryPrimitives.ReadUInt64BigEndian(info.AsSpan(10, 8));
                var sampleRate = packed >> 44;
                var bits = ((packed >> 36) & 31) + 1;
                if (minBlock < 16 || maxBlock < minBlock || sampleRate == 0 || bits is < 4 or > 32)
                    return false;
            }
            stream.Position = next;
            if (!last) continue;
            if (stream.Length - stream.Position < 8) return false;
            var frame = Read(stream, 4);
            return frame[0] == 0xff && (frame[1] & 0xfe) == 0xf8
                && (frame[2] >> 4) != 0 && (frame[2] & 15) != 15
                && (frame[3] >> 4) <= 10 && ((frame[3] >> 1) & 7) != 3
                && (frame[3] & 1) == 0;
        }
        return false;
    }

    private static bool IsMp3(Stream stream, CancellationToken cancellationToken)
    {
        long start = 0;
        var tag = Read(stream, 10);
        if (tag.AsSpan(0, 3).SequenceEqual("ID3"u8))
        {
            if (tag[3] is < 2 or > 4 || tag.AsSpan(6, 4).ContainsAnyInRange((byte)128, byte.MaxValue))
                return false;
            var size = (tag[6] << 21) | (tag[7] << 14) | (tag[8] << 7) | tag[9];
            start = 10L + size + (tag[3] == 4 && (tag[5] & 0x10) != 0 ? 10 : 0);
        }
        var end = Math.Min(start + 64 * 1024, stream.Length - 4);
        for (var offset = start; offset <= end; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long next = offset;
            var firstVersion = -1;
            var firstRate = -1;
            var frames = 0;
            for (; frames < 3; frames++)
            {
                if (next + 4 > stream.Length) break;
                stream.Position = next;
                var frame = Read(stream, 4);
                var version = (frame[1] >> 3) & 3;
                var layer = (frame[1] >> 1) & 3;
                var bitrateIndex = frame[2] >> 4;
                var rateIndex = (frame[2] >> 2) & 3;
                if (frame[0] != 0xff || (frame[1] & 0xe0) != 0xe0
                    || version == 1 || layer != 1 || bitrateIndex is 0 or 15 || rateIndex == 3
                    || (frame[3] & 3) == 2) break;
                int[] v1Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
                int[] v2Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
                int[] rates = [44100, 48000, 32000];
                var rate = rates[rateIndex] / (version == 3 ? 1 : version == 2 ? 2 : 4);
                var bitrate = (version == 3 ? v1Bitrates : v2Bitrates)[bitrateIndex] * 1000;
                if (frames != 0 && (version != firstVersion || rate != firstRate)) break;
                firstVersion = version;
                firstRate = rate;
                var frameLength = (version == 3 ? 144 : 72) * bitrate / rate + ((frame[2] >> 1) & 1);
                if (next + frameLength > stream.Length) break;
                next += frameLength;
            }
            if (frames == 3) return true;
        }
        return false;
    }

    private static byte[] Read(Stream stream, int count)
    {
        var bytes = new byte[count];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
