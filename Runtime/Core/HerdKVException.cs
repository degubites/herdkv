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
public class HerdKVException : Exception
{
    public HerdKVException(string message)
        : base(message)
    {
    }

    public HerdKVException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
}
