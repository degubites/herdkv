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
public sealed class HerdKVMigrationException : HerdKVException
{
    public HerdKVMigrationException(string message)
        : base(message)
    {
    }

    public HerdKVMigrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
}
