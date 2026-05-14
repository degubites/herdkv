# HerdKV

HerdKV is a Unity-first embedded key-value store for save data, caches, and frequently updated local game state.

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
- [.NET Console Usage](Documentation~/manual/dotnet-console.md)
- [Public API](Documentation~/manual/public-api.md)
- [File Format](Documentation~/manual/file-format.md)
- [Crash Recovery](Documentation~/manual/crash-recovery.md)
- [Codecs](Documentation~/manual/codecs.md)
- [Migrations](Documentation~/manual/migrations.md)
- [Editor Viewer](Documentation~/manual/editor-viewer.md)
- [Performance](Documentation~/manual/performance.md)
- [Benchmarks](Documentation~/manual/benchmarks.md)
- [Samples](Documentation~/manual/samples.md)
- [Limitations](Documentation~/manual/limitations.md)
- [FAQ](Documentation~/manual/faq.md)
