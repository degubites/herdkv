# Limitations

HerdKV is intentionally small and local.

It is not:

- a SQL database
- a query engine
- a Unity object serializer
- a cloud save service
- a multi-process database
- a full ACID transaction engine

Use one process per database path. Store your own serialized payloads when saving complex objects. `ListKeysAsync` supports sorted live-key prefix listing, but HerdKV does not inspect values or maintain automatic secondary indexes.

## Durability and ACID Scope

HerdKV serializes operations inside one store instance and writes append-only records. On startup it scans segment files, validates records, and truncates incomplete or corrupt tail data.

Current guarantees are intentionally narrow:

- single-key writes and deletes are appended as individual records
- batch writes are coalesced for performance but are not multi-key transactions
- incomplete tail records are ignored during recovery
- `FlushAsync()` pushes the active writer to the file system and updates the manifest
- multi-key transactions are not supported
- multiple processes or multiple independent store instances should not write to the same database path
- power-loss durability depends on the OS and storage device; HerdKV does not currently force fsync/write-through semantics

For games, call `FlushAsync()` at explicit save points such as checkpoints, scene transitions, application pause, and quit.

See [Durability](durability.md) for the detailed policy.
