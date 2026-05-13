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
internal readonly struct IndexEntry
{
    public IndexEntry(int segmentId, long offset, int keySizeBytes, int valueSizeBytes, int recordSizeBytes)
    {
        SegmentId = segmentId;
        Offset = offset;
        KeySizeBytes = keySizeBytes;
        ValueSizeBytes = valueSizeBytes;
        RecordSizeBytes = recordSizeBytes;
    }

    public int SegmentId { get; }

    public long Offset { get; }

    public int KeySizeBytes { get; }

    public int ValueSizeBytes { get; }

    public int RecordSizeBytes { get; }
}
}
