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
public sealed class BooleanCodec : IHerdKVCodec<bool>
{
    public byte[] Encode(bool value)
    {
        return new[] { value ? (byte)1 : (byte)0 };
    }

    public bool Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != 1 || bytes.Span[0] > 1)
        {
            throw new HerdKVCodecException("Boolean values must be one byte with value 0 or 1.", new FormatException());
        }

        return bytes.Span[0] == 1;
    }
}
}
