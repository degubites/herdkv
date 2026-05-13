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
[Flags]
internal enum HerdKVRecordFlags : byte
{
    None = 0,
    Tombstone = 1
}
}
