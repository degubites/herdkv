# Performance Notes

HerdKV includes a dependency-free benchmark project at `Development~/benchmarks/HerdKV.Benchmarks`.
See [Benchmarks](benchmarks.md) for the current baseline measurements and reproduction command.

Known allocation sources:

- keys are UTF-8 encoded for each write/delete
- `GetAsync` returns a new byte array for successful reads
- checksum-on-read validates record bytes before returning values
- fast reads with `VerifyChecksumOnRead=false` keep values in memory and return copies
- batch writes copy values into a coalesced operation list before appending records
- codec APIs allocate according to their encoding format

## Read Modes

`VerifyChecksumOnRead=true` is the default safety-oriented mode. Reads go back to the segment file and validate the stored CRC.

`VerifyChecksumOnRead=false` is the fast local gameplay mode. The store builds an in-memory value cache while opening and writing the database, then serves reads from memory. Startup recovery still validates records while scanning the append-only log, but individual reads skip disk verification.

Use the fast mode when your game needs frequent reads of local save state and you can flush at clear save points. Use checksum-on-read when you are inspecting data, debugging corruption, or prioritizing verification over runtime read speed.

## Write Batches

`WriteBatchAsync` is the preferred path for many related changes made in one frame or save step. The batch is coalesced by key before any records are appended, so repeated writes to the same hot key keep only the final operation.

```csharp
await db.WriteBatchAsync(new[]
{
    HerdKVBatchOperation.Put("player/gold", 100, HerdKVCodecs.Int32),
    HerdKVBatchOperation.Put("player/gold", 250, HerdKVCodecs.Int32),
    HerdKVBatchOperation.Put("quest/intro", true, HerdKVCodecs.Boolean)
});
```

This is useful for frame-end state snapshots, inventory updates, settings pages, and hot keys such as currency or counters. It is not a multi-key ACID transaction; if a process dies during the append, recovery may observe a prefix of the written records.

## Flush Modes

`HerdKVOptions.FlushMode` controls how aggressively writes are flushed.

- `Manual`: default. Writes are buffered by the active writer and flushed on `FlushAsync`, dispose, segment rollover, compaction, and safety-mode reads.
- `FlushOnWrite`: flushes after each `PutAsync`, `DeleteAsync`, or completed batch.
- `WriteThrough`: opens the active writer with `FileOptions.WriteThrough` and also flushes after each write operation.

For most Unity games, `Manual` plus explicit `FlushAsync()` at save points is the best performance profile. Use `FlushOnWrite` or `WriteThrough` only when the extra durability is worth the I/O cost.

See [Durability](durability.md) for the exact save-point policy and crash recovery semantics.

Unity-specific profiling should be done in the Unity Profiler before making release performance claims.
