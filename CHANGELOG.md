# Changelog

## 0.10.1 - 2026-05-17

- Added root Korean README for GitHub and OpenUPM mirrors.
- Documented Codeberg-only issue tracking and surfaced tutorial links near the README top.

## 0.10.0 - 2026-05-17

- Added `HerdKVFlushMode` and `HerdKVOptions.FlushMode` with `Manual`, `FlushOnWrite`, and `WriteThrough` modes.
- Added `HerdKVBatchOperation` and `IHerdKVStore.WriteBatchAsync` for coalesced batch writes.
- Added `IHerdKVStore.ListKeysAsync` for sorted live-key prefix listing.
- Added English and Korean Unity save tutorials covering primitives, serializer-owned JSON DTOs, prefix listing, batch writes, and flush points.
- Coalesced repeated keys inside one batch so hot-key updates write only the final operation.
- Changed manifest writes to use an atomic temp-file replacement flow.
- Added tests for batch writes, hot-key coalescing, flush-on-write visibility, and manifest temp-file recovery.
- Documented the `FlushAsync()` durability boundary and flush mode policy.
- Updated performance and benchmark documentation for batch writes and flush modes.

## 0.9.0

- Added UPM sample metadata.
- Added Unity samples for Auto Save, Inventory Save, World Chunk Save, Settings Save, and Migration Example.
- Added a .NET console sample.
- Added Getting Started, Unity Installation, .NET Console Usage, Limitations, FAQ, Samples, and Packaging docs.
- Added package documentation index under `Documentation~`.
- Expanded README installation and navigation sections.

## 0.8.0

- Added `HerdKVOpenOptions` with schema version and migration configuration.
- Added `IHerdKVMigration`.
- Added schema version storage through `HerdKVSchema`.
- Added migration backup and rollback on migration failure.
- Added migration-aware Core and Unity open overloads.
- Added save slot migration overloads.
- Added migration tests for sequential migrations, rollback, missing migration failure, and newer database rejection.

## 0.7.0

- Added read-only Core inspection APIs for listing live keys and reading inspected values.
- Added `Window > HerdKV > Viewer` Editor window.
- Added key search, metadata table, UTF-8/hex preview, delete, export, compact, and integrity actions.
- Added inspector tests for live key listing, value reads, corrupt tail warnings, and stale segment ignoring.

## 0.6.0

- Removed the unconditional value copy from the Core `PutAsync` path.
- Reduced read-with-checksum allocation by validating header/key/value directly instead of allocating a whole-record buffer and copying the value.
- Reused pooled value buffers during startup recovery scans.
- Added a standalone benchmark console project.
- Added performance notes and baseline benchmark results.

## 0.5.0

- Added crash recovery hardening tests for partial key tails, partial value tails, CRC mismatch tails, partial tombstone tails, and interrupted compaction leftovers.
- Documented the current tail corruption recovery policy.
- Verified previous committed data remains readable after corrupted tail records.

## 0.4.0

- Added Unity runtime assembly.
- Added `HerdKVUnity.OpenAsync` and `HerdKVUnity.OpenSlotAsync`.
- Added `Application.persistentDataPath` database path helpers.
- Added `HerdKVSettings` ScriptableObject settings.
- Added `HerdKVAutoFlush` for pause and quit flushing.
- Added a Basic Save sample.

## 0.3.0

- Added `IHerdKVCodec<T>` for typed values.
- Added typed `PutAsync`, `GetAsync`, and `GetRequiredAsync` extension methods.
- Added `BytesCodec`, `StringUtf8Codec`, `Int32Codec`, `Int64Codec`, `SingleCodec`, and `BooleanCodec`.
- Added codec decode error handling through `HerdKVCodecException`.
- Added codec round-trip and error tests.

## 0.2.0

- Added segment rollover based on `HerdKVOptions.SegmentSizeBytes`.
- Added manual `CompactAsync`.
- Added manifest-based active segment tracking.
- Added cleanup of ignored old segment files after compaction.
- Added tests for rollover, compaction shrink behavior, delete preservation, and stale segment ignoring.

## 0.1.0

- Added initial Unity package metadata.
- Added the minimal Core byte-array API.
- Added a single-segment append-only storage engine with CRC validation and startup recovery.
