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
public static class HerdKVStoreCodecExtensions
{
    public static ValueTask PutAsync<T>(
        this IHerdKVStore store,
        string key,
        T value,
        IHerdKVCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        return store.PutAsync(key, codec.Encode(value), cancellationToken);
    }

    public static async ValueTask<T?> GetAsync<T>(
        this IHerdKVStore store,
        string key,
        IHerdKVCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await store.GetAsync(key, cancellationToken);
        if (bytes is null)
        {
            return default;
        }

        try
        {
            return codec.Decode(bytes);
        }
        catch (HerdKVCodecException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HerdKVCodecException($"Failed to decode value for key '{key}'.", ex);
        }
    }

    public static async ValueTask<T> GetRequiredAsync<T>(
        this IHerdKVStore store,
        string key,
        IHerdKVCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await store.GetAsync(key, cancellationToken);
        if (bytes is null)
        {
            throw new KeyNotFoundException($"Key '{key}' was not found.");
        }

        try
        {
            return codec.Decode(bytes);
        }
        catch (HerdKVCodecException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HerdKVCodecException($"Failed to decode value for key '{key}'.", ex);
        }
    }
}
}
