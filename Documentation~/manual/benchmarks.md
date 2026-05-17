# Benchmarks

HerdKV benchmarks are early engineering data. Use them to compare scenarios on the same machine, not as absolute cross-device performance claims.

## Unity Editor Comparison

Measured in Unity 6000.0.65f1 Editor Play Mode on Windows.

Hardware:

- CPU: AMD Ryzen 7 5700X3D
- RAM: 64 GB
- Storage: Samsung PM981a NVMe SSD

Settings:

- Easy Save: JSON format, no encryption, no compression
- HerdKV: `VerifyChecksumOnRead=false` for fast local reads
- Dataset: `itemCount=500`, `randomReadCount=1000`, `hotUpdateCount=1000`, `chunkCount=64`, `chunkSizeBytes=4096`
- Iterations: 3

Median results:

| Scenario | JSON full file | Easy Save | HerdKV | Notes |
|---|---:|---:|---:|---|
| Full object save+load | 20.1 ms | 45.9 ms | 56.0 ms | HerdKV stores one JSON blob, so this is not its strongest case. |
| Many small keys write+random read | n/a | 6,404 ms | 2,963 ms | HerdKV avoids rewriting one large object. |
| Hot key repeated update | 10,750 ms | 2,811 ms | 5,872 ms | Append-only storage keeps old versions until compaction. |
| Chunk byte payloads write+random read | n/a | 7,546 ms | 428 ms | HerdKV is strongest with independent binary payloads. |

Interpretation:

- HerdKV is most useful for many independent keys and binary/chunk-style payloads.
- Plain JSON full-file saves are still very competitive for small whole-save snapshots.
- Easy Save is strong for tiny hot-key overwrite cases because the final file can remain extremely small.
- Hot-key workloads should be paired with periodic `CompactAsync()` if storage growth matters.

## .NET Microbenchmarks

Measured on Windows 10 with .NET 8.0.25 in a Release run.

| Scenario | Operations | Elapsed | Ops/s | Allocated/op |
|---|---:|---:|---:|---:|
| Put | 1,000 | 14.63 ms | 68,336 | 536 B |
| Put | 10,000 | 20.70 ms | 483,026 | 504 B |
| Get hot safe | 5,000 | 2,459.11 ms | 2,033 | 5,813 B |
| Get hot fast | 5,000 | 0.37 ms | 13,383,298 | 40 B |
| Get random safe | 5,000 | 2,216.22 ms | 2,256 | 5,896 B |
| Get random fast | 5,000 | 0.98 ms | 5,106,731 | 88 B |
| Startup recovery | 10,000 | 35.00 ms | 285,694 | 435 B |
| Repeated write | 10,000 | 18.11 ms | 552,209 | 319 B |
| Compact hot key | 1 | 11.33 ms | 88 | 19,752 B |
| Batch repeated write | 10,000 | 10.31 ms | 970,299 | 73 B |

`safe` means the default `VerifyChecksumOnRead=true` mode. Reads go back to the segment file and validate CRC.

`fast` means `VerifyChecksumOnRead=false`. The store keeps values in memory and returns copied bytes on reads. This is usually the better mode for Unity gameplay data when startup recovery and explicit flush points are enough.

`Batch repeated write` uses `WriteBatchAsync` with 10,000 updates to the same key. Batch coalescing writes only the final value, leaving `47 B` total storage, `47 B` live storage, and `0 B` dead storage in this run.

## Reproduce

```text
dotnet run -c Release --project Development~/benchmarks/HerdKV.Benchmarks/HerdKV.Benchmarks.csproj
```

Unity results should be measured in the Unity Profiler or with a small scene-local benchmark runner because Editor load, OS disk cache, storage device, antivirus, and background applications can noticeably affect file I/O timings.
