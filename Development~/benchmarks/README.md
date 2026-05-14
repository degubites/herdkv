# HerdKV Benchmarks

Run from the repository root:

```text
dotnet run -c Release --project Development~/benchmarks/HerdKV.Benchmarks/HerdKV.Benchmarks.csproj
```

The benchmark uses `Stopwatch`, `GC.GetTotalAllocatedBytes`, and generation collection counts. It is intentionally dependency-free so it can run offline.

For formal performance claims, run on a quiet machine, use Release builds, repeat several times, and record the hardware, runtime, OS, and package version.
