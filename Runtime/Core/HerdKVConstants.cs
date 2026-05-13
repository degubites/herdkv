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
internal static class HerdKVConstants
{
    public const int HeaderSize = 26;
    public const int Magic = 0x314B5648;
    public const byte Version = 1;
    public const string ManifestFileName = "MANIFEST";
    public const string SegmentExtension = ".hseg";
}
}
