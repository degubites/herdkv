using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Text;

namespace Degubites.HerdKV
{
public static class HerdKVInspector
{
    public static async ValueTask<HerdKVInspectionReport> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        var entries = new Dictionary<string, HerdKVInspectionEntry>(StringComparer.Ordinal);
        var warnings = new List<string>();
        long totalBytes = 0;

        (int minSegment, int activeSegment) = ReadManifest(path);
        for (int segmentId = minSegment; segmentId <= activeSegment; segmentId++)
        {
            string segmentPath = GetSegmentPath(path, segmentId);
            if (!File.Exists(segmentPath))
            {
                continue;
            }

            await using var file = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long offset = 0;
            while (offset < file.Length)
            {
                long recordOffset = offset;
                byte[] headerBytes = new byte[HerdKVConstants.HeaderSize];
                int headerRead = await ReadAtMostAsync(file, headerBytes, cancellationToken);
                if (headerRead != HerdKVConstants.HeaderSize || !HerdKVRecord.TryReadHeader(headerBytes, out HerdKVRecordHeader header))
                {
                    warnings.Add($"Corrupt tail in {Path.GetFileName(segmentPath)} at offset {recordOffset}.");
                    break;
                }

                if (recordOffset + header.RecordLength > file.Length)
                {
                    warnings.Add($"Partial record in {Path.GetFileName(segmentPath)} at offset {recordOffset}.");
                    break;
                }

                byte[] record = new byte[header.RecordLength];
                Buffer.BlockCopy(headerBytes, 0, record, 0, headerBytes.Length);
                int payloadLength = header.RecordLength - HerdKVConstants.HeaderSize;
                int payloadRead = await ReadAtMostAsync(file, record.AsMemory(HerdKVConstants.HeaderSize, payloadLength), cancellationToken);
                if (payloadRead != payloadLength || !HerdKVRecord.Validate(record, out header))
                {
                    warnings.Add($"CRC mismatch in {Path.GetFileName(segmentPath)} at offset {recordOffset}.");
                    break;
                }

                string key = Encoding.UTF8.GetString(record, HerdKVConstants.HeaderSize, header.KeyLength);
                if ((header.Flags & HerdKVRecordFlags.Tombstone) == HerdKVRecordFlags.Tombstone)
                {
                    entries.Remove(key);
                }
                else
                {
                    entries[key] = new HerdKVInspectionEntry(
                        key,
                        Path.GetFileName(segmentPath),
                        recordOffset,
                        header.ValueLength,
                        header.RecordLength);
                }

                totalBytes += header.RecordLength;
                offset += header.RecordLength;
            }
        }

        return new HerdKVInspectionReport(entries.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray(), warnings, totalBytes);
    }

    public static async ValueTask<byte[]?> ReadValueAsync(
        string path,
        HerdKVInspectionEntry entry,
        CancellationToken cancellationToken = default)
    {
        string segmentPath = Path.Combine(path, entry.SegmentFileName);
        if (!File.Exists(segmentPath))
        {
            return null;
        }

        await using var file = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        file.Position = entry.Offset;
        byte[] record = new byte[entry.RecordSizeBytes];
        int read = await ReadAtMostAsync(file, record, cancellationToken);
        if (read != record.Length || !HerdKVRecord.Validate(record, out HerdKVRecordHeader header))
        {
            throw new HerdKVCorruptRecordException($"Inspected record '{entry.Key}' is corrupt.");
        }

        byte[] value = new byte[header.ValueLength];
        Buffer.BlockCopy(record, HerdKVConstants.HeaderSize + header.KeyLength, value, 0, value.Length);
        return value;
    }

    private static (int MinSegment, int ActiveSegment) ReadManifest(string path)
    {
        int minSegment = 1;
        int activeSegment = Math.Max(1, Directory.Exists(path) ? GetHighestSegmentId(path) : 1);
        string manifestPath = Path.Combine(path, HerdKVConstants.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return (minSegment, activeSegment);
        }

        foreach (string line in File.ReadAllLines(manifestPath))
        {
            string[] parts = line.Split('=');
            if (parts.Length != 2)
            {
                continue;
            }

            if (parts[0] == "minSegment" && int.TryParse(parts[1], out int parsedMin))
            {
                minSegment = Math.Max(1, parsedMin);
            }
            else if (parts[0] == "activeSegment" && int.TryParse(parts[1], out int parsedActive))
            {
                activeSegment = Math.Max(1, parsedActive);
            }
        }

        return (minSegment, activeSegment);
    }

    private static int GetHighestSegmentId(string path)
    {
        int highest = 0;
        foreach (string file in Directory.EnumerateFiles(path, "*" + HerdKVConstants.SegmentExtension))
        {
            if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int segmentId))
            {
                highest = Math.Max(highest, segmentId);
            }
        }

        return highest;
    }

    private static string GetSegmentPath(string path, int segmentId)
    {
        return Path.Combine(path, $"{segmentId:D6}{HerdKVConstants.SegmentExtension}");
    }

    private static async ValueTask<int> ReadAtMostAsync(FileStream file, byte[] buffer, CancellationToken cancellationToken)
    {
        return await ReadAtMostAsync(file, buffer.AsMemory(), cancellationToken);
    }

    private static async ValueTask<int> ReadAtMostAsync(FileStream file, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await file.ReadAsync(buffer.Slice(totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
}
