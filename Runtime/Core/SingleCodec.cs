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
public sealed class SingleCodec : IHerdKVCodec<float>
{
    public byte[] Encode(float value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(value));
        return bytes;
    }

    public float Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != 4)
        {
            throw new HerdKVCodecException("Single values must be exactly 4 bytes.", new FormatException());
        }

        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Span));
    }
}
}
