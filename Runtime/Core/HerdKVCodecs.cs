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
public static class HerdKVCodecs
{
    public static BytesCodec Bytes { get; } = new();

    public static StringUtf8Codec StringUtf8 { get; } = new();

    public static Int32Codec Int32 { get; } = new();

    public static Int64Codec Int64 { get; } = new();

    public static SingleCodec Single { get; } = new();

    public static BooleanCodec Boolean { get; } = new();
}
}
