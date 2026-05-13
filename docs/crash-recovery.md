# Crash Recovery

Startup scans active segments from the manifest and keeps records until the first invalid tail.

Recovered cases:

- partial header tails
- partial key or value tails
- CRC mismatch tails
- tombstone tails
- stale segments below `minSegment`

When a tail record is incomplete or fails CRC validation during startup, HerdKV truncates the segment back to the last valid record.
