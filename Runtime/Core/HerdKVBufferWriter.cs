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
internal sealed class HerdKVBufferWriter
{
    private readonly MemoryStream _stream = new();

    public void Write(byte[] bytes)
    {
        _stream.Write(bytes, 0, bytes.Length);
    }

    public byte[] ToArray()
    {
        return _stream.ToArray();
    }
}
}
