# Public API

Core entry points:

- `HerdKVStore.OpenAsync` opens a database by filesystem path.
- `IHerdKVStore.PutAsync` appends or overwrites a value.
- `IHerdKVStore.GetAsync` reads a value as bytes.
- `IHerdKVStore.DeleteAsync` writes a tombstone.
- `IHerdKVStore.FlushAsync` flushes pending writes.
- `IHerdKVStore.CompactAsync` rewrites live records into fresh segments.
- `IHerdKVStore.GetStats` reports live, dead, and total bytes.

Unity helpers:

- `HerdKVUnity.OpenAsync` opens a database under `Application.persistentDataPath/HerdKV`.
- `HerdKVUnity.OpenSlotAsync` opens a save-slot path.
- `HerdKVSettings` stores default open options in a `ScriptableObject`.
- `HerdKVAutoFlush` flushes a store on pause and quit.
