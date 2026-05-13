# Benchmarks

Measured on Windows 10 with .NET 8.0.25 in a Release run.

These numbers are early baselines, not release claims.

| Scenario | Operations | Elapsed | Ops/s | Allocated/op |
|---|---:|---:|---:|---:|
| Put | 1,000 | 18.71 ms | 53,452 | 584 B |
| Put | 10,000 | 123.18 ms | 81,184 | 549 B |
| Get hot key | 5,000 | 11.61 ms | 430,656 | 305 B |
| Get random key | 5,000 | 121.48 ms | 41,161 | 1,021 B |
| Startup recovery | 10,000 | 81.29 ms | 123,019 | 537 B |
| Repeated write | 10,000 | 113.98 ms | 87,736 | 360 B |
| Compact hot key | 1 | 18.57 ms | 54 | 16,640 B |

## Notes

- `PutAsync` no longer copies the input value memory before writing.
- Checksum-on-read still allocates the returned value array, as required by the public byte-array API.
- Startup recovery reuses pooled value buffers while scanning records.
- Key encoding currently allocates per operation.
- String interpolation and benchmark key generation contribute to benchmark allocations.
- Unity performance still needs measurement in the Unity Profiler.

## Reproduce

```text
dotnet run -c Release --project benchmarks/HerdKV.Benchmarks/HerdKV.Benchmarks.csproj
```
