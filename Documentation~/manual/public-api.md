# Public API

Core entry points:

- `HerdKVStore.OpenAsync` opens a database by filesystem path.
- `IHerdKVStore.PutAsync` appends or overwrites a value.
- `IHerdKVStore.WriteBatchAsync` writes a coalesced batch of put/delete operations under one store lock.
- `IHerdKVStore.GetAsync` reads a value as bytes.
- `IHerdKVStore.ListKeysAsync` returns sorted live keys with an optional prefix filter.
- `IHerdKVStore.DeleteAsync` writes a tombstone.
- `IHerdKVStore.FlushAsync` flushes pending writes.
- `IHerdKVStore.CompactAsync` rewrites live records into fresh segments.
- `IHerdKVStore.GetStats` reports live, dead, and total bytes.
- `HerdKVBatchOperation.Put` and `HerdKVBatchOperation.Delete` build batch operations.
- `HerdKVOptions.FlushMode` selects `Manual`, `FlushOnWrite`, or `WriteThrough` write flushing.

Batch writes coalesce repeated keys before records are appended. If a batch contains ten `Put` operations for the same key, only the final value is written. A batch is a performance feature, not a multi-key ACID transaction.

Prefix key listing uses ordinal string ordering:

```csharp
IReadOnlyList<string> equipmentKeys = await db.ListKeysAsync("equipment/");
IReadOnlyList<string> outboxKeys = await db.ListKeysAsync("sync/outbox/");
```

`ListKeysAsync` only lists live keys. Filtering by rarity, location, owner, or other value fields should be modeled with your own secondary index keys.

Unity helpers:

- `HerdKVUnity.OpenAsync` opens a database under `Application.persistentDataPath/HerdKV`.
- `HerdKVUnity.OpenSlotAsync` opens a save-slot path.
- `HerdKVSettings` stores default open options in a `ScriptableObject`.
- `HerdKVAutoFlush` flushes a store on pause and quit.
