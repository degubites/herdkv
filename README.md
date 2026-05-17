# HerdKV

HerdKV is a Unity-first embedded key-value store for save data, caches, and frequently updated local game state.

Korean README: [README_ko.md](README_ko.md)

Tutorials: [Unity Save Tutorial](Documentation~/manual/unity-save-tutorial.md) / [Unity Save Tutorial (Korean)](Documentation~/manual/unity-save-tutorial.ko.md)

Issues and feature requests are tracked on Codeberg only: [codeberg.org/degubites/herdkv/issues](https://codeberg.org/degubites/herdkv/issues). GitHub is a read-only mirror for OpenUPM distribution.

## Install

Install through Unity Package Manager using a local path or the package git URL:

```text
Packages/com.degubites.herdkv
```

```text
https://codeberg.org/degubites/herdkv.git
```

```csharp
await using var db = await HerdKVStore.OpenAsync("./save");

await db.PutAsync("player/name", Encoding.UTF8.GetBytes("Alice"));

byte[]? value = await db.GetAsync("player/name");

await db.PutAsync("player/level", 42, HerdKVCodecs.Int32);

int level = await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32);

await db.DeleteAsync("player/name");

await db.CompactAsync();

await db.FlushAsync();
```

## Scope

HerdKV focuses on local byte-array key-value storage with append-only persistence, CRC validation, startup recovery, delete tombstones, segment rollover, manual compaction, and codec-based typed values.

It is not a SQL database, query engine, Unity object serializer, cloud save service, or multi-process database.

## When to Use HerdKV

HerdKV is a good fit when your game stores many independent local values and wants to update them without rewriting one large save file.

Good use cases:

- world chunks keyed by coordinate, region, scene, or shard
- inventory slots, item stacks, quest flags, and progression state
- local player settings and feature flags
- caches for generated data, thumbnails, metadata, or downloaded content
- append-friendly data where crash recovery matters more than SQL-style queries

It is usually not the right tool for:

- relational queries, filtering, sorting, joins, or reporting
- Unity object graph serialization by reference
- cloud synchronization or conflict resolution
- multi-process access to the same database path
- full ACID transactions across multiple keys

HerdKV serializes operations inside one store instance and can recover incomplete or corrupt tail records on startup. It does not currently provide multi-key transactions, multi-process isolation, or fsync-level power-loss guarantees. Call `FlushAsync()` at save points such as checkpoints, scene transitions, pause, and quit.

Durability policy: HerdKV treats `FlushAsync()` as the application-level durability boundary. `PutAsync`, `DeleteAsync`, and `WriteBatchAsync` update the store and append records, but in the default `Manual` mode they are not documented as completed save points until the active writer is flushed. See [Durability](Documentation~/manual/durability.md).

For many changes at once, use `WriteBatchAsync`. Repeated keys are coalesced before records are appended, which makes it useful for hot keys such as currency, counters, settings, and frame-end state snapshots.

```csharp
await db.WriteBatchAsync(new[]
{
    HerdKVBatchOperation.Put("player/gold", 250, HerdKVCodecs.Int32),
    HerdKVBatchOperation.Put("quest/intro", true, HerdKVCodecs.Boolean),
    HerdKVBatchOperation.Delete("cache/old-thumbnail")
});
```

Prefix key listing is available for lightweight local indexes:

```csharp
IReadOnlyList<string> equipmentKeys = await db.ListKeysAsync("equipment/");
IReadOnlyList<string> outboxKeys = await db.ListKeysAsync("sync/outbox/");
```

Use explicit secondary index keys for value-based queries such as rarity, location, or equipped character.

## Unity

```csharp
await using var db = await HerdKVUnity.OpenAsync("main-save");

await db.PutAsync("player/name", "Alice", HerdKVCodecs.StringUtf8);
```

Databases opened through `HerdKVUnity` are created under `Application.persistentDataPath/HerdKV`.

Save slots:

```csharp
await using var slot = await HerdKVUnity.OpenSlotAsync("slot-1");
```

Migrations:

```csharp
var options = new HerdKVOpenOptions { SchemaVersion = 2 };
options.Migrations.Add(new V0ToV1Migration());
options.Migrations.Add(new V1ToV2Migration());

await using var migrated = await HerdKVStore.OpenAsync("./save", options);
```

## Benchmarks

Reference Unity Editor benchmark environment:

- CPU: AMD Ryzen 7 5700X3D
- RAM: 64 GB
- Storage: Samsung PM981a NVMe SSD
- OS: Windows
- Unity: 6000.0.65f1, Editor Play Mode
- Easy Save: JSON format, no encryption, no compression
- HerdKV: `VerifyChecksumOnRead=false` for fast local reads

Representative median results from the same-machine Unity comparison:

| Scenario | JSON full file | Easy Save | HerdKV |
|---|---:|---:|---:|
| Full object save+load | 20.1 ms | 45.9 ms | 56.0 ms |
| Many small keys write+random read | n/a | 6,404 ms | 2,963 ms |
| Hot key repeated update | 10,750 ms | 2,811 ms | 5,872 ms |
| Chunk byte payloads write+random read | n/a | 7,546 ms | 428 ms |

These numbers are relative comparison data from one machine, not cross-device performance claims. See [Benchmarks](Documentation~/manual/benchmarks.md) for details and .NET microbenchmarks.

```text
dotnet run -c Release --project Development~/benchmarks/HerdKV.Benchmarks/HerdKV.Benchmarks.csproj
```

## Tests

```text
dotnet run --project Development~/tests/Degubites.HerdKV.Tests/Degubites.HerdKV.Tests.csproj
```

## Editor Viewer

Open `Window > HerdKV > Viewer` in the Unity Editor to inspect local HerdKV databases.

## Docs

- [Getting Started](Documentation~/manual/getting-started.md)
- [Unity Installation](Documentation~/manual/unity-installation.md)
- [Unity Save Tutorial](Documentation~/manual/unity-save-tutorial.md)
- [Unity Save Tutorial (Korean)](Documentation~/manual/unity-save-tutorial.ko.md)
- [.NET Console Usage](Documentation~/manual/dotnet-console.md)
- [Public API](Documentation~/manual/public-api.md)
- [File Format](Documentation~/manual/file-format.md)
- [Crash Recovery](Documentation~/manual/crash-recovery.md)
- [Durability](Documentation~/manual/durability.md)
- [Codecs](Documentation~/manual/codecs.md)
- [Migrations](Documentation~/manual/migrations.md)
- [Editor Viewer](Documentation~/manual/editor-viewer.md)
- [Performance](Documentation~/manual/performance.md)
- [Benchmarks](Documentation~/manual/benchmarks.md)
- [Samples](Documentation~/manual/samples.md)
- [Limitations](Documentation~/manual/limitations.md)
- [FAQ](Documentation~/manual/faq.md)
