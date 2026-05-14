# Performance Notes

HerdKV includes a dependency-free benchmark project at `Development~/benchmarks/HerdKV.Benchmarks`.
See [Benchmarks](benchmarks.md) for the current baseline measurements and reproduction command.

Known allocation sources:

- keys are UTF-8 encoded for each write/delete
- `GetAsync` returns a new byte array for successful reads
- checksum-on-read validates record bytes before returning values
- fast reads with `VerifyChecksumOnRead=false` keep values in memory and return copies
- codec APIs allocate according to their encoding format

## Read Modes

`VerifyChecksumOnRead=true` is the default safety-oriented mode. Reads go back to the segment file and validate the stored CRC.

`VerifyChecksumOnRead=false` is the fast local gameplay mode. The store builds an in-memory value cache while opening and writing the database, then serves reads from memory. Startup recovery still validates records while scanning the append-only log, but individual reads skip disk verification.

Use the fast mode when your game needs frequent reads of local save state and you can flush at clear save points. Use checksum-on-read when you are inspecting data, debugging corruption, or prioritizing verification over runtime read speed.

Unity-specific profiling should be done in the Unity Profiler before making release performance claims.
