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
public sealed class HerdKVInspectionReport
{
    public HerdKVInspectionReport(IReadOnlyList<HerdKVInspectionEntry> entries, IReadOnlyList<string> warnings, long totalBytes)
    {
        Entries = entries;
        Warnings = warnings;
        TotalBytes = totalBytes;
        LiveBytes = entries.Sum(entry => (long)entry.RecordSizeBytes);
        DeadBytes = Math.Max(0, totalBytes - LiveBytes);
    }

    public IReadOnlyList<HerdKVInspectionEntry> Entries { get; }

    public IReadOnlyList<string> Warnings { get; }

    public int KeyCount => Entries.Count;

    public long TotalBytes { get; }

    public long LiveBytes { get; }

    public long DeadBytes { get; }

    public bool HasWarnings => Warnings.Count > 0;
}
}
