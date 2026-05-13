# File Format

HerdKV uses append-only segment files named `000001.hseg`, `000002.hseg`, and so on.

Each record has a 26-byte header followed by UTF-8 key bytes and value bytes:

- magic
- version
- flags
- key length
- value length
- sequence
- CRC32

Deletes are stored as tombstone records. `MANIFEST` stores the active segment range used on startup. Segments below `minSegment` are ignored so stale data cannot be resurrected after compaction.
