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
public sealed class HerdKVCodecException : HerdKVException
{
    public HerdKVCodecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
}
