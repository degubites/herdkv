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
public sealed class BytesCodec : IHerdKVCodec<byte[]>
{
    public byte[] Encode(byte[] value)
    {
        return value.ToArray();
    }

    public byte[] Decode(ReadOnlyMemory<byte> bytes)
    {
        return bytes.ToArray();
    }
}
}
