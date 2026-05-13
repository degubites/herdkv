using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;

namespace Degubites.HerdKV
{
internal static class HerdKVRecord
{
    public static int GetRecordLength(int keyLength, int valueLength)
    {
        return HerdKVConstants.HeaderSize + keyLength + valueLength;
    }

    public static byte[] Create(byte[] keyBytes, ReadOnlySpan<byte> value, HerdKVRecordFlags flags, long sequence)
    {
        int recordLength = GetRecordLength(keyBytes.Length, value.Length);
        byte[] record = new byte[recordLength];
        Span<byte> span = record;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), HerdKVConstants.Magic);
        span[4] = HerdKVConstants.Version;
        span[5] = (byte)flags;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(6, 4), keyBytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(10, 4), value.Length);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(14, 8), sequence);

        keyBytes.CopyTo(span.Slice(HerdKVConstants.HeaderSize, keyBytes.Length));
        value.CopyTo(span.Slice(HerdKVConstants.HeaderSize + keyBytes.Length, value.Length));

        uint crc = Crc32.Compute(ConcatForCrc(span.Slice(0, 22), span.Slice(HerdKVConstants.HeaderSize)));
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(22, 4), crc);

        return record;
    }

    public static bool TryReadHeader(ReadOnlySpan<byte> headerBytes, out HerdKVRecordHeader header)
    {
        header = default;

        if (headerBytes.Length != HerdKVConstants.HeaderSize)
        {
            return false;
        }

        int magic = BinaryPrimitives.ReadInt32LittleEndian(headerBytes.Slice(0, 4));
        if (magic != HerdKVConstants.Magic || headerBytes[4] != HerdKVConstants.Version)
        {
            return false;
        }

        int keyLength = BinaryPrimitives.ReadInt32LittleEndian(headerBytes.Slice(6, 4));
        int valueLength = BinaryPrimitives.ReadInt32LittleEndian(headerBytes.Slice(10, 4));
        if (keyLength <= 0 || valueLength < 0)
        {
            return false;
        }

        long sequence = BinaryPrimitives.ReadInt64LittleEndian(headerBytes.Slice(14, 8));
        uint crc = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(22, 4));
        header = new HerdKVRecordHeader((HerdKVRecordFlags)headerBytes[5], keyLength, valueLength, sequence, crc);
        return true;
    }

    public static bool Validate(ReadOnlySpan<byte> record, out HerdKVRecordHeader header)
    {
        if (record.Length < HerdKVConstants.HeaderSize)
        {
            header = default;
            return false;
        }

        if (!TryReadHeader(record.Slice(0, HerdKVConstants.HeaderSize), out header))
        {
            return false;
        }

        if (record.Length != header.RecordLength)
        {
            return false;
        }

        uint computed = Crc32.Compute(ConcatForCrc(record.Slice(0, 22), record.Slice(HerdKVConstants.HeaderSize)));
        return computed == header.Crc;
    }

    private static byte[] ConcatForCrc(ReadOnlySpan<byte> headerPrefix, ReadOnlySpan<byte> payload)
    {
        byte[] bytes = new byte[headerPrefix.Length + payload.Length];
        headerPrefix.CopyTo(bytes);
        payload.CopyTo(bytes.AsSpan(headerPrefix.Length));
        return bytes;
    }
}
}
