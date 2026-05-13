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
internal static class HerdKVKey
{
    public static byte[] Encode(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key must not be null or empty.", nameof(key));
        }

        return Encoding.UTF8.GetBytes(key);
    }

    public static string Decode(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }
}
}
