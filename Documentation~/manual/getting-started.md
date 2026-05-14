# Getting Started

Open a database, write values, read them back, and flush when you need durable data on disk.

```csharp
await using IHerdKVStore db = await HerdKVStore.OpenAsync("./save");

await db.PutAsync("player/name", "Alice", HerdKVCodecs.StringUtf8);
await db.PutAsync("player/level", 7, HerdKVCodecs.Int32);

string? name = await db.GetAsync("player/name", HerdKVCodecs.StringUtf8);
int level = await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32);

await db.FlushAsync();
```

HerdKV stores byte-array values. Codecs provide typed convenience on top of the byte API.
