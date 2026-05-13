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
public sealed class HerdKVInspectionEntry
{
    public HerdKVInspectionEntry(string key, string segmentFileName, long offset, int valueSizeBytes, int recordSizeBytes)
    {
        Key = key;
        SegmentFileName = segmentFileName;
        Offset = offset;
        ValueSizeBytes = valueSizeBytes;
        RecordSizeBytes = recordSizeBytes;
    }

    public string Key { get; }

    public string SegmentFileName { get; }

    public long Offset { get; }

    public int ValueSizeBytes { get; }

    public int RecordSizeBytes { get; }
}
}
