using System;

namespace Degubites.HerdKV
{
public sealed class HerdKVBatchOperation
{
    private HerdKVBatchOperation(string key, ReadOnlyMemory<byte> value, bool isDelete)
    {
        Key = key;
        Value = value;
        IsDelete = isDelete;
    }

    public string Key { get; }

    public ReadOnlyMemory<byte> Value { get; }

    public bool IsDelete { get; }

    public static HerdKVBatchOperation Put(string key, ReadOnlyMemory<byte> value)
    {
        return new HerdKVBatchOperation(key, value, isDelete: false);
    }

    public static HerdKVBatchOperation Put<T>(string key, T value, IHerdKVCodec<T> codec)
    {
        return Put(key, codec.Encode(value));
    }

    public static HerdKVBatchOperation Delete(string key)
    {
        return new HerdKVBatchOperation(key, ReadOnlyMemory<byte>.Empty, isDelete: true);
    }
}
}
