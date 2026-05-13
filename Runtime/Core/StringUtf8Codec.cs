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
public sealed class StringUtf8Codec : IHerdKVCodec<string>
{
    public byte[] Encode(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    public string Decode(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes.Span);
        }
        catch (Exception ex)
        {
            throw new HerdKVCodecException("Failed to decode UTF-8 string value.", ex);
        }
    }
}
}
