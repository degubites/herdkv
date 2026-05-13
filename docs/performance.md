# Performance Notes

HerdKV includes a dependency-free benchmark project at `benchmarks/HerdKV.Benchmarks`.
See [Benchmarks](benchmarks.md) for the current baseline measurements and reproduction command.

Known allocation sources:

- keys are UTF-8 encoded for each write/delete
- `GetAsync` returns a new byte array for successful reads
- checksum-on-read validates record bytes before returning values
- codec APIs allocate according to their encoding format

Unity-specific profiling should be done in the Unity Profiler before making release performance claims.
