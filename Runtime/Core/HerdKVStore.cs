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
public static class HerdKVStore
{
    public static ValueTask<IHerdKVStore> OpenAsync(
        string path,
        HerdKVOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return AppendOnlyHerdKVStore.OpenAsync(path, options ?? new HerdKVOptions(), cancellationToken);
    }

    public static async ValueTask<IHerdKVStore> OpenAsync(
        string path,
        HerdKVOpenOptions options,
        CancellationToken cancellationToken = default)
    {
        IHerdKVStore store = await AppendOnlyHerdKVStore.OpenAsync(path, options.StorageOptions, cancellationToken);

        try
        {
            await HerdKVMigrationRunner.RunAsync(path, store, options, cancellationToken);
            return store;
        }
        catch
        {
            await store.DisposeAsync();
            throw;
        }
    }
}
}
