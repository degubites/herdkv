# Durability Policy

HerdKV treats `FlushAsync()` as the application-level durability boundary.

`PutAsync`, `DeleteAsync`, and `WriteBatchAsync` update the store state and append records to the active writer. In the default `Manual` flush mode, callers should not treat those writes as a completed save point until `FlushAsync()` completes, the store is disposed, or another operation explicitly flushes the active writer.

## Operation Semantics

| Operation | Semantics |
|---|---|
| `PutAsync` completed | The in-memory index is updated and a record has been written to the active writer. In `Manual` mode this is not a durable save point by itself. |
| `DeleteAsync` completed | A tombstone record has been written to the active writer and the key is removed from the in-memory index. In `Manual` mode this is not a durable save point by itself. |
| `WriteBatchAsync` completed | The batch has been coalesced by key and appended under one store lock. It is a performance feature, not a multi-key transaction. |
| `FlushAsync` completed | The active writer has been flushed and the manifest has been written through the atomic manifest path. This is HerdKV's normal save-point boundary. |
| `DisposeAsync` completed | The active writer has been flushed and closed. Prefer an explicit `FlushAsync()` at important save points instead of relying only on disposal. |

## Flush Modes

`HerdKVOptions.FlushMode` controls when HerdKV flushes writes.

| Mode | Behavior | Typical Use |
|---|---|---|
| `Manual` | Default. Writes are buffered by the active writer and flushed on `FlushAsync`, dispose, segment rollover, compaction, and checksum-on-read disk reads. | Most Unity gameplay saves. |
| `FlushOnWrite` | Flushes after every `PutAsync`, `DeleteAsync`, and completed `WriteBatchAsync`. | Small saves where immediate visibility is more important than write throughput. |
| `WriteThrough` | Opens the active writer with `FileOptions.WriteThrough` and flushes after write operations. | More conservative local file writes when the extra I/O cost is acceptable. |

`WriteThrough` is a platform and file-system hint, not a promise that every storage device will survive sudden power loss with the latest write intact.

## Crash Recovery

HerdKV stores records in append-only segment files. When a database opens, HerdKV scans the manifest-selected segment range, validates records, and rebuilds the in-memory index.

Recovery behavior:

- complete valid records are replayed
- tombstones delete keys during replay
- incomplete tail records are truncated
- corrupt tail records are truncated
- stale segment files outside the manifest range are ignored
- leftover manifest temp or backup files are ignored

If a process dies while appending a batch, recovery may observe only a prefix of the records that reached the segment file. Batch writes are coalesced before append, but they are not atomic transactions.

## Atomic Manifest Writes

Manifest updates use a temporary-file replacement flow:

1. write `MANIFEST.tmp`
2. flush the temp file
3. replace or move it into `MANIFEST`
4. remove the backup file when the platform creates one

This keeps the manifest from being rewritten in place. If a crash leaves `MANIFEST.tmp` or `MANIFEST.bak`, HerdKV ignores those files and reads `MANIFEST`.

## Recommended Unity Pattern

Use `Manual` mode for normal gameplay performance and call `FlushAsync()` at clear save points:

- after explicit player save
- after checkpoint completion
- before scene transition
- on application pause
- on application quit

For autosave components, call `FlushAsync()` from pause/quit handlers. For settings screens or small critical saves, consider `FlushOnWrite`.

## Non-Goals

HerdKV does not currently provide:

- multi-key ACID transactions
- multi-process write coordination
- fsync-level guarantees across all platforms
- cloud synchronization or conflict resolution
- relational constraints, queries, joins, or indexes
