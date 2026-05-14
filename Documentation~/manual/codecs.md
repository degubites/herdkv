# Codecs

Codecs turn typed values into byte arrays.

Built-in codecs:

- `HerdKVCodecs.Bytes`
- `HerdKVCodecs.StringUtf8`
- `HerdKVCodecs.Int32`
- `HerdKVCodecs.Int64`
- `HerdKVCodecs.Single`
- `HerdKVCodecs.Boolean`

```csharp
await db.PutAsync("player/level", 7, HerdKVCodecs.Int32);
int level = await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32);
```

Implement `IHerdKVCodec<T>` for custom value formats.
