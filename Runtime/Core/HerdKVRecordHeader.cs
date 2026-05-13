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
internal readonly struct HerdKVRecordHeader
{
    public HerdKVRecordHeader(HerdKVRecordFlags flags, int keyLength, int valueLength, long sequence, uint crc)
    {
        Flags = flags;
        KeyLength = keyLength;
        ValueLength = valueLength;
        Sequence = sequence;
        Crc = crc;
    }

    public HerdKVRecordFlags Flags { get; }

    public int KeyLength { get; }

    public int ValueLength { get; }

    public long Sequence { get; }

    public uint Crc { get; }

    public int RecordLength => HerdKVConstants.HeaderSize + KeyLength + ValueLength;
}
}
