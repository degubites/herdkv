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
public static class HerdKVSchema
{
    public const string VersionKey = "__herdkv/schema-version";

    public static async ValueTask<int> GetVersionAsync(IHerdKVStore store, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await store.GetAsync(VersionKey, cancellationToken);
        return bytes is null ? 0 : HerdKVCodecs.Int32.Decode(bytes);
    }

    public static ValueTask SetVersionAsync(IHerdKVStore store, int version, CancellationToken cancellationToken = default)
    {
        return store.PutAsync(VersionKey, version, HerdKVCodecs.Int32, cancellationToken);
    }
}
}
