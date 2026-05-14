using System.Runtime.CompilerServices;
using System.Text;
using Degubites.HerdKV;

internal static class Program
{
    private static readonly (string Name, Func<Task> Test)[] Tests =
    {
        ("PutGetDelete", PutGetDelete),
        ("DataSurvivesReopen", DataSurvivesReopen),
        ("DeleteSurvivesReopen", DeleteSurvivesReopen),
        ("LastWriteWinsAfterReopen", LastWriteWinsAfterReopen),
        ("PutAfterGetDoesNotOverwrite", PutAfterGetDoesNotOverwrite),
        ("CorruptedTailIsTruncated", CorruptedTailIsTruncated),
        ("ChecksumMismatchIsDetectedOnRead", ChecksumMismatchIsDetectedOnRead),
        ("StatsTrackLiveAndDeadBytes", StatsTrackLiveAndDeadBytes),
        ("SegmentRolloverSurvivesReopen", SegmentRolloverSurvivesReopen),
        ("CompactionShrinksRepeatedWrites", CompactionShrinksRepeatedWrites),
        ("CompactionPreservesDeletes", CompactionPreservesDeletes),
        ("ManifestIgnoresStaleSegmentsAfterCompaction", ManifestIgnoresStaleSegmentsAfterCompaction),
        ("StringCodecRoundTripSurvivesReopen", StringCodecRoundTripSurvivesReopen),
        ("PrimitiveCodecsRoundTrip", PrimitiveCodecsRoundTrip),
        ("BytesCodecCopiesInput", BytesCodecCopiesInput),
        ("MissingTypedReferenceReturnsNull", MissingTypedReferenceReturnsNull),
        ("CodecDecodeErrorsSurfaceAsCodecExceptions", CodecDecodeErrorsSurfaceAsCodecExceptions),
        ("PartialKeyTailKeepsPreviousData", PartialKeyTailKeepsPreviousData),
        ("PartialValueTailKeepsPreviousData", PartialValueTailKeepsPreviousData),
        ("CrcMismatchTailKeepsPreviousData", CrcMismatchTailKeepsPreviousData),
        ("PartialTombstoneTailLeavesPreviousValue", PartialTombstoneTailLeavesPreviousValue),
        ("InterruptedCompactionSegmentsDoNotCorruptOpen", InterruptedCompactionSegmentsDoNotCorruptOpen),
        ("InspectorListsKeysAndReadsValues", InspectorListsKeysAndReadsValues),
        ("InspectorReportsCorruptTailWarnings", InspectorReportsCorruptTailWarnings),
        ("InspectorIgnoresStaleSegmentsAfterCompaction", InspectorIgnoresStaleSegmentsAfterCompaction),
        ("MigrationsRunSequentiallyAndStoreVersion", MigrationsRunSequentiallyAndStoreVersion),
        ("FailedMigrationRestoresBackup", FailedMigrationRestoresBackup),
        ("MissingMigrationFailsWithoutChangingData", MissingMigrationFailsWithoutChangingData),
        ("OlderTargetVersionIsRejected", OlderTargetVersionIsRejected)
    };

    public static async Task<int> Main()
    {
        int failed = 0;

        foreach ((string name, Func<Task> test) in Tests)
        {
            try
            {
                await test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {name}");
                Console.WriteLine(ex);
            }
        }

        Console.WriteLine($"{Tests.Length - failed}/{Tests.Length} tests passed");
        return failed == 0 ? 0 : 1;
    }

    private static async Task PutGetDelete()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

            await db.PutAsync("player/name", Bytes("Alice"));
            AssertBytes("Alice", await db.GetAsync("player/name"));

            bool deleted = await db.DeleteAsync("player/name");
            AssertTrue(deleted, "Delete should return true for an existing key.");
            AssertNull(await db.GetAsync("player/name"), "Deleted key should not be returned.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task DataSurvivesReopen()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("settings/music", Bytes("on"));
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertBytes("on", await reopened.GetAsync("settings/music"));
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task DeleteSurvivesReopen()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("quest/1", Bytes("started"));
                await db.DeleteAsync("quest/1");
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertNull(await reopened.GetAsync("quest/1"), "Deleted key should stay deleted after reopen.");
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task LastWriteWinsAfterReopen()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("player/level", Bytes("1"));
                await db.PutAsync("player/level", Bytes("2"));
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertBytes("2", await reopened.GetAsync("player/level"));
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task CorruptedTailIsTruncated()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stable/key", Bytes("stable"));
            }

            string segmentPath = Path.Combine(path, "000001.hseg");
            long validLength = new FileInfo(segmentPath).Length;
            await using (var file = new FileStream(segmentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                byte[] tailBytes = { 1, 2, 3, 4, 5 };
                await file.WriteAsync(tailBytes, 0, tailBytes.Length);
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertBytes("stable", await reopened.GetAsync("stable/key"));
            }

            long recoveredLength = new FileInfo(segmentPath).Length;
            AssertEqual(validLength, recoveredLength, "Corrupted tail bytes should be truncated.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task PutAfterGetDoesNotOverwrite()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("a", Bytes("1"));
                await db.PutAsync("b", Bytes("2"));
                AssertBytes("1", await db.GetAsync("a"));
                await db.PutAsync("c", Bytes("3"));
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertBytes("1", await reopened.GetAsync("a"));
                AssertBytes("2", await reopened.GetAsync("b"));
                AssertBytes("3", await reopened.GetAsync("c"));
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task ChecksumMismatchIsDetectedOnRead()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);
            await db.PutAsync("crc/key", Bytes("good"));
            await db.FlushAsync();

            string segmentPath = Path.Combine(path, "000001.hseg");
            int valueOffset = 26 + Encoding.UTF8.GetByteCount("crc/key");
            await using (var file = new FileStream(segmentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.Position = valueOffset;
                file.WriteByte((byte)'X');
                await file.FlushAsync();
            }

            await AssertThrowsAsync<HerdKVCorruptRecordException>(async () => await db.GetAsync("crc/key"));
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task StatsTrackLiveAndDeadBytes()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);
            await db.PutAsync("a", Bytes("1"));
            await db.PutAsync("b", Bytes("2"));
            HerdKVStats initial = db.GetStats();

            AssertEqual(2, initial.KeyCount, "Two live keys should be indexed.");
            AssertTrue(initial.LiveBytes > 0, "Live bytes should increase after writes.");
            AssertEqual(0L, initial.DeadBytes, "Fresh writes should not have dead bytes.");

            await db.PutAsync("a", Bytes("3"));
            await db.DeleteAsync("b");
            HerdKVStats afterChanges = db.GetStats();

            AssertEqual(1, afterChanges.KeyCount, "One key should remain after overwrite and delete.");
            AssertTrue(afterChanges.DeadBytes > 0, "Overwrites and tombstones should create dead bytes.");
            AssertBytes("3", await db.GetAsync("a"));
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task SegmentRolloverSurvivesReopen()
    {
        string path = CreateStorePath();
        var options = new HerdKVOptions { SegmentSizeBytes = 80 };

        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path, options))
            {
                await db.PutAsync("k/1", Bytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
                await db.PutAsync("k/2", Bytes("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
                await db.PutAsync("k/3", Bytes("cccccccccccccccccccccccccccccccccccccccc"));

                HerdKVStats stats = db.GetStats();
                AssertTrue(stats.SegmentCount >= 3, "Small segment size should roll over into multiple segments.");
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path, options))
            {
                AssertBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", await reopened.GetAsync("k/1"));
                AssertBytes("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", await reopened.GetAsync("k/2"));
                AssertBytes("cccccccccccccccccccccccccccccccccccccccc", await reopened.GetAsync("k/3"));
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task CompactionShrinksRepeatedWrites()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

            for (int i = 0; i < 20; i++)
            {
                await db.PutAsync("hot/key", Bytes($"value-{i:D2}"));
            }

            HerdKVStats before = db.GetStats();
            AssertTrue(before.DeadBytes > 0, "Repeated writes should create dead bytes before compaction.");

            await db.CompactAsync();
            HerdKVStats after = db.GetStats();

            AssertBytes("value-19", await db.GetAsync("hot/key"));
            AssertEqual(1, after.KeyCount, "Compaction should preserve the live key.");
            AssertEqual(0L, after.DeadBytes, "Compaction should remove dead bytes from active segments.");
            AssertTrue(after.TotalBytes < before.TotalBytes, "Compaction should shrink active storage.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task CompactionPreservesDeletes()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("live/key", Bytes("yes"));
                await db.PutAsync("deleted/key", Bytes("no"));
                await db.DeleteAsync("deleted/key");
                await db.CompactAsync();
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertBytes("yes", await reopened.GetAsync("live/key"));
                AssertNull(await reopened.GetAsync("deleted/key"), "Deleted keys should stay deleted after compaction and reopen.");
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task ManifestIgnoresStaleSegmentsAfterCompaction()
    {
        string path = CreateStorePath();
        byte[] staleSegmentBytes;

        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stale/key", Bytes("old"));
                await db.FlushAsync();

                staleSegmentBytes = await ReadAllBytesSharedAsync(Path.Combine(path, "000001.hseg"));

                await db.DeleteAsync("stale/key");
                await db.PutAsync("live/key", Bytes("new"));
                await db.CompactAsync();
            }

            File.WriteAllBytes(Path.Combine(path, "000001.hseg"), staleSegmentBytes);

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                AssertNull(await reopened.GetAsync("stale/key"), "Manifest should ignore stale lower-numbered segments after compaction.");
                AssertBytes("new", await reopened.GetAsync("live/key"));
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task StringCodecRoundTripSurvivesReopen()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("player/title", "Captain Alice", HerdKVCodecs.StringUtf8);
            }

            await using (IHerdKVStore reopened = await HerdKVStore.OpenAsync(path))
            {
                string? title = await reopened.GetAsync("player/title", HerdKVCodecs.StringUtf8);
                AssertEqual("Captain Alice", title!, "String codec value should survive reopen.");
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task PrimitiveCodecsRoundTrip()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

            await db.PutAsync("player/level", 42, HerdKVCodecs.Int32);
            await db.PutAsync("economy/gold", 9000000000L, HerdKVCodecs.Int64);
            await db.PutAsync("player/speed", 3.5f, HerdKVCodecs.Single);
            await db.PutAsync("settings/music", true, HerdKVCodecs.Boolean);

            AssertEqual(42, await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32), "Int32 codec round-trip failed.");
            AssertEqual(9000000000L, await db.GetRequiredAsync("economy/gold", HerdKVCodecs.Int64), "Int64 codec round-trip failed.");
            AssertClose(3.5f, await db.GetRequiredAsync("player/speed", HerdKVCodecs.Single), "Single codec round-trip failed.");
            AssertTrue(await db.GetRequiredAsync("settings/music", HerdKVCodecs.Boolean), "Boolean codec round-trip failed.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task BytesCodecCopiesInput()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

            byte[] original = { 1, 2, 3 };
            await db.PutAsync("blob", original, HerdKVCodecs.Bytes);
            original[0] = 9;

            byte[] stored = await db.GetRequiredAsync("blob", HerdKVCodecs.Bytes);
            AssertEqual((byte)1, stored[0], "Bytes codec should not expose the caller's mutable input array.");
            AssertEqual((byte)2, stored[1], "Bytes codec round-trip changed byte 1.");
            AssertEqual((byte)3, stored[2], "Bytes codec round-trip changed byte 2.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task MissingTypedReferenceReturnsNull()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);
            string? value = await db.GetAsync("missing/string", HerdKVCodecs.StringUtf8);
            AssertNull(value, "Missing typed reference values should return null.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task CodecDecodeErrorsSurfaceAsCodecExceptions()
    {
        string path = CreateStorePath();
        try
        {
            await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

            await db.PutAsync("bad/int", new byte[] { 1, 2, 3 });
            await db.PutAsync("bad/bool", new byte[] { 2 });

            await AssertThrowsAsync<HerdKVCodecException>(async () => await db.GetRequiredAsync("bad/int", HerdKVCodecs.Int32));
            await AssertThrowsAsync<HerdKVCodecException>(async () => await db.GetRequiredAsync("bad/bool", HerdKVCodecs.Boolean));
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task PartialKeyTailKeepsPreviousData()
    {
        string path = CreateStorePath();
        try
        {
            long stableLength;
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stable/key", Bytes("stable"));
                await db.FlushAsync();
                stableLength = new FileInfo(Path.Combine(path, "000001.hseg")).Length;

                await db.PutAsync("tail/key", Bytes("tail"));
                await db.FlushAsync();
            }

            string segmentPath = Path.Combine(path, "000001.hseg");
            SetFileLength(segmentPath, stableLength + RecordHeaderSize + 2);

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("stable", await reopened.GetAsync("stable/key"));
            AssertNull(await reopened.GetAsync("tail/key"), "Partial key tail should be ignored.");
            AssertEqual(stableLength, new FileInfo(segmentPath).Length, "Partial key tail should be truncated.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task PartialValueTailKeepsPreviousData()
    {
        string path = CreateStorePath();
        try
        {
            long stableLength;
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stable/key", Bytes("stable"));
                await db.FlushAsync();
                stableLength = new FileInfo(Path.Combine(path, "000001.hseg")).Length;

                await db.PutAsync("tail/key", Bytes("tail-value"));
                await db.FlushAsync();
            }

            string segmentPath = Path.Combine(path, "000001.hseg");
            int tailKeyLength = Encoding.UTF8.GetByteCount("tail/key");
            SetFileLength(segmentPath, stableLength + RecordHeaderSize + tailKeyLength + 2);

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("stable", await reopened.GetAsync("stable/key"));
            AssertNull(await reopened.GetAsync("tail/key"), "Partial value tail should be ignored.");
            AssertEqual(stableLength, new FileInfo(segmentPath).Length, "Partial value tail should be truncated.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task CrcMismatchTailKeepsPreviousData()
    {
        string path = CreateStorePath();
        try
        {
            long stableLength;
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stable/key", Bytes("stable"));
                await db.FlushAsync();
                stableLength = new FileInfo(Path.Combine(path, "000001.hseg")).Length;

                await db.PutAsync("tail/key", Bytes("tail"));
                await db.FlushAsync();
            }

            string segmentPath = Path.Combine(path, "000001.hseg");
            int tailKeyLength = Encoding.UTF8.GetByteCount("tail/key");
            OverwriteByte(segmentPath, stableLength + RecordHeaderSize + tailKeyLength, (byte)'X');

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("stable", await reopened.GetAsync("stable/key"));
            AssertNull(await reopened.GetAsync("tail/key"), "CRC mismatch tail should be ignored.");
            AssertEqual(stableLength, new FileInfo(segmentPath).Length, "CRC mismatch tail should be truncated.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task PartialTombstoneTailLeavesPreviousValue()
    {
        string path = CreateStorePath();
        try
        {
            long valueLength;
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("player/name", Bytes("Alice"));
                await db.FlushAsync();
                valueLength = new FileInfo(Path.Combine(path, "000001.hseg")).Length;

                await db.DeleteAsync("player/name");
                await db.FlushAsync();
            }

            string segmentPath = Path.Combine(path, "000001.hseg");
            SetFileLength(segmentPath, valueLength + RecordHeaderSize + 3);

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("Alice", await reopened.GetAsync("player/name"));
            AssertEqual(valueLength, new FileInfo(segmentPath).Length, "Partial tombstone tail should be truncated.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task InterruptedCompactionSegmentsDoNotCorruptOpen()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("live/key", Bytes("yes"));
                await db.PutAsync("deleted/key", Bytes("no"));
                await db.DeleteAsync("deleted/key");
                await db.CompactAsync();
            }

            File.WriteAllText(Path.Combine(path, "MANIFEST"), "version=1\nminSegment=1\nactiveSegment=1\n");

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("yes", await reopened.GetAsync("live/key"));
            AssertNull(await reopened.GetAsync("deleted/key"), "Orphan compacted segments should not resurrect deleted data.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task InspectorListsKeysAndReadsValues()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("player/name", Bytes("Alice"));
                await db.PutAsync("player/level", Bytes("42"));
                await db.PutAsync("deleted/key", Bytes("gone"));
                await db.DeleteAsync("deleted/key");
            }

            HerdKVInspectionReport report = await HerdKVInspector.InspectAsync(path);
            AssertEqual(2, report.KeyCount, "Inspector should list live keys only.");
            AssertTrue(report.TotalBytes > 0, "Inspector should report total bytes.");
            AssertTrue(report.DeadBytes > 0, "Inspector should report dead bytes after delete.");

            HerdKVInspectionEntry nameEntry = FindEntry(report, "player/name");
            AssertEqual("000001.hseg", nameEntry.SegmentFileName, "Inspector should report the segment file.");
            AssertEqual(5, nameEntry.ValueSizeBytes, "Inspector should report value size.");

            byte[]? value = await HerdKVInspector.ReadValueAsync(path, nameEntry);
            AssertBytes("Alice", value);
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task InspectorReportsCorruptTailWarnings()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stable/key", Bytes("stable"));
            }

            string segmentPath = Path.Combine(path, "000001.hseg");
            await using (var file = new FileStream(segmentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                byte[] tailBytes = { 1, 2, 3 };
                await file.WriteAsync(tailBytes, 0, tailBytes.Length);
            }

            HerdKVInspectionReport report = await HerdKVInspector.InspectAsync(path);
            AssertEqual(1, report.KeyCount, "Inspector should keep valid keys before corrupt tail.");
            AssertTrue(report.HasWarnings, "Inspector should report corrupt tail warnings.");
            AssertBytes("stable", await HerdKVInspector.ReadValueAsync(path, FindEntry(report, "stable/key")));
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task InspectorIgnoresStaleSegmentsAfterCompaction()
    {
        string path = CreateStorePath();
        byte[] staleSegmentBytes;

        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("stale/key", Bytes("old"));
                await db.FlushAsync();
                staleSegmentBytes = await ReadAllBytesSharedAsync(Path.Combine(path, "000001.hseg"));

                await db.DeleteAsync("stale/key");
                await db.PutAsync("live/key", Bytes("new"));
                await db.CompactAsync();
            }

            File.WriteAllBytes(Path.Combine(path, "000001.hseg"), staleSegmentBytes);

            HerdKVInspectionReport report = await HerdKVInspector.InspectAsync(path);
            AssertEqual(1, report.KeyCount, "Inspector should ignore stale lower-numbered segments.");
            AssertNull(FindEntryOrNull(report, "stale/key"), "Inspector should not list stale keys.");
            AssertBytes("new", await HerdKVInspector.ReadValueAsync(path, FindEntry(report, "live/key")));
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task MigrationsRunSequentiallyAndStoreVersion()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("player/name", Bytes("Alice"));
            }

            var options = new HerdKVOpenOptions { SchemaVersion = 2 };
            options.Migrations.Add(new AddLevelMigration());
            options.Migrations.Add(new RenamePlayerNameMigration());

            await using (IHerdKVStore migrated = await HerdKVStore.OpenAsync(path, options))
            {
                AssertEqual(2, await HerdKVSchema.GetVersionAsync(migrated), "Schema version should be stored after migration.");
                AssertBytes("Alice", await migrated.GetAsync("player/display-name"));
                AssertBytes("1", await migrated.GetAsync("player/level"));
                AssertNull(await migrated.GetAsync("player/name"), "Renamed key should be deleted by migration.");
            }
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task FailedMigrationRestoresBackup()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("safe/key", Bytes("safe"));
            }

            var options = new HerdKVOpenOptions { SchemaVersion = 1 };
            options.Migrations.Add(new FailingMigration());

            await AssertThrowsAsync<HerdKVMigrationException>(async () => await OpenAndDisposeAsync(path, options));

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("safe", await reopened.GetAsync("safe/key"));
            AssertNull(await reopened.GetAsync("bad/key"), "Failed migration writes should be rolled back.");
            AssertEqual(0, await HerdKVSchema.GetVersionAsync(reopened), "Failed migration should not advance schema version.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task MissingMigrationFailsWithoutChangingData()
    {
        string path = CreateStorePath();
        try
        {
            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("safe/key", Bytes("safe"));
            }

            var options = new HerdKVOpenOptions { SchemaVersion = 1 };
            await AssertThrowsAsync<HerdKVMigrationException>(async () => await OpenAndDisposeAsync(path, options));

            await using IHerdKVStore reopened = await HerdKVStore.OpenAsync(path);
            AssertBytes("safe", await reopened.GetAsync("safe/key"));
            AssertEqual(0, await HerdKVSchema.GetVersionAsync(reopened), "Missing migration should not advance schema version.");
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static async Task OlderTargetVersionIsRejected()
    {
        string path = CreateStorePath();
        try
        {
            var createOptions = new HerdKVOpenOptions { SchemaVersion = 2 };
            createOptions.Migrations.Add(new AddLevelMigration());
            createOptions.Migrations.Add(new RenamePlayerNameMigration());

            await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
            {
                await db.PutAsync("player/name", Bytes("Alice"));
            }

            await using (await HerdKVStore.OpenAsync(path, createOptions))
            {
            }

            var oldOptions = new HerdKVOpenOptions { SchemaVersion = 1 };
            await AssertThrowsAsync<HerdKVMigrationException>(async () => await OpenAndDisposeAsync(path, oldOptions));
        }
        finally
        {
            DeleteStorePath(path);
        }
    }

    private static byte[] Bytes(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    private static async Task OpenAndDisposeAsync(string path, HerdKVOpenOptions options)
    {
        await using IHerdKVStore db = await HerdKVStore.OpenAsync(path, options);
    }

    private const int RecordHeaderSize = 26;

    private static async Task<byte[]> ReadAllBytesSharedAsync(string path)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        return memory.ToArray();
    }

    private static void SetFileLength(string path, long length)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        file.SetLength(length);
    }

    private static void OverwriteByte(string path, long offset, byte value)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        file.Position = offset;
        file.WriteByte(value);
    }

    private static string CreateStorePath([CallerMemberName] string? testName = null)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData");
        string path = Path.Combine(root, $"{testName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteStorePath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void AssertBytes(string expected, byte[]? actual)
    {
        AssertNotNull(actual, $"Expected '{expected}' but got null.");
        string actualText = Encoding.UTF8.GetString(actual!);
        AssertEqual(expected, actualText, "Unexpected byte value.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertNull(object? value, string message)
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }

    private static HerdKVInspectionEntry FindEntry(HerdKVInspectionReport report, string key)
    {
        HerdKVInspectionEntry? entry = FindEntryOrNull(report, key);
        if (entry is null)
        {
            throw new InvalidOperationException($"Inspector entry '{key}' was not found.");
        }

        return entry;
    }

    private static HerdKVInspectionEntry? FindEntryOrNull(HerdKVInspectionReport report, string key)
    {
        foreach (HerdKVInspectionEntry entry in report.Entries)
        {
            if (entry.Key == key)
            {
                return entry;
            }
        }

        return null;
    }

    private static void AssertClose(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.0001f)
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}.", ex);
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    private sealed class AddLevelMigration : IHerdKVMigration
    {
        public int FromVersion => 0;

        public int ToVersion => 1;

        public async ValueTask MigrateAsync(IHerdKVStore db)
        {
            await db.PutAsync("player/level", Bytes("1"));
        }
    }

    private sealed class RenamePlayerNameMigration : IHerdKVMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public async ValueTask MigrateAsync(IHerdKVStore db)
        {
            byte[]? name = await db.GetAsync("player/name");
            if (name is not null)
            {
                await db.PutAsync("player/display-name", name);
                await db.DeleteAsync("player/name");
            }
        }
    }

    private sealed class FailingMigration : IHerdKVMigration
    {
        public int FromVersion => 0;

        public int ToVersion => 1;

        public async ValueTask MigrateAsync(IHerdKVStore db)
        {
            await db.PutAsync("bad/key", Bytes("bad"));
            throw new InvalidOperationException("Migration failed intentionally.");
        }
    }
}
