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
public readonly struct HerdKVStats
{
    public HerdKVStats(int keyCount, int segmentCount, long totalBytes, long liveBytes, long deadBytes)
    {
        KeyCount = keyCount;
        SegmentCount = segmentCount;
        TotalBytes = totalBytes;
        LiveBytes = liveBytes;
        DeadBytes = deadBytes;
    }

    public int KeyCount { get; }

    public int SegmentCount { get; }

    public long TotalBytes { get; }

    public long LiveBytes { get; }

    public long DeadBytes { get; }
}
}
