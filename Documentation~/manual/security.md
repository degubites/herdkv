# Security Notes

HerdKV is a local embedded storage library. It is designed for save data, caches, and local game state, not for secret storage or cheat-proof persistence.

## Threat Model

HerdKV can help with:

- detecting accidental record corruption with CRC validation
- recovering from incomplete or corrupt tail records after crashes
- keeping writes under `Application.persistentDataPath` when using `HerdKVUnity.OpenAsync` or `OpenSlotAsync`

HerdKV does not provide:

- encryption at rest
- tamper-proof saves
- authentication or signing
- secure cloud synchronization
- protection from a user who can freely edit local files
- multi-process isolation for one database path

## Checksums Are Not Signatures

Record CRCs are for corruption detection. They can catch accidental damage, partial writes, and many disk-level problems.

CRCs are not a security feature. A user or tool that understands the file format can modify a record and recompute the CRC.

If you need tamper evidence, add your own signature or MAC over the serialized payload or over a higher-level save manifest.

## Encryption

HerdKV stores bytes as provided by the caller. If save data needs encryption, encrypt before `PutAsync` and decrypt after `GetAsync`.

Key management is outside HerdKV. Avoid shipping a hardcoded encryption key and expecting it to protect offline single-player data from determined local inspection.

## Paths

Prefer Unity helpers for game saves:

```csharp
await using IHerdKVStore db = await HerdKVUnity.OpenAsync("main-save");
```

Unity database and slot names are sanitized as single path parts. Names that resolve to `.` or `..` are rejected.

The lower-level `HerdKVStore.OpenAsync(path)` API accepts a raw filesystem path. Treat that path as trusted application configuration. Do not pass untrusted user input directly to it.

## Serializer Responsibility

HerdKV does not deserialize objects by itself. If you store JSON, MessagePack, MemoryPack, protobuf, or a custom binary format, validate the data at your serialization boundary.

Avoid deserializing untrusted payloads into types with side effects. Prefer simple DTOs for save data.

## Editor Viewer

The Editor Viewer is an Editor-only inspection tool. It can export and delete selected keys from the opened database. Do not expose it in runtime player builds.
