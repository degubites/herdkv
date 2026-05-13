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
public sealed class Int64Codec : IHerdKVCodec<long>
{
    public byte[] Encode(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes;
    }

    public long Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != 8)
        {
            throw new HerdKVCodecException("Int64 values must be exactly 8 bytes.", new FormatException());
        }

        return BinaryPrimitives.ReadInt64LittleEndian(bytes.Span);
    }
}
}
