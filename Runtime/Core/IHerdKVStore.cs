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
public interface IHerdKVStore : IAsyncDisposable
{
    ValueTask PutAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    ValueTask WriteBatchAsync(IEnumerable<HerdKVBatchOperation> operations, CancellationToken cancellationToken = default);

    ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    ValueTask CompactAsync(CancellationToken cancellationToken = default);

    HerdKVStats GetStats();
}
}
