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
public sealed class Int32Codec : IHerdKVCodec<int>
{
    public byte[] Encode(int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    public int Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != 4)
        {
            throw new HerdKVCodecException("Int32 values must be exactly 4 bytes.", new FormatException());
        }

        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span);
    }
}
}
