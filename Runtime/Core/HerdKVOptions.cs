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
public sealed class HerdKVOptions
{
    public long SegmentSizeBytes { get; set; } = 4 * 1024 * 1024;

    public bool VerifyChecksumOnRead { get; set; } = true;

    public HerdKVFlushMode FlushMode { get; set; } = HerdKVFlushMode.Manual;
}
}
