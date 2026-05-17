using System.Diagnostics;
using System.Text;
using Degubites.HerdKV;

internal static class Program
{
    private const int SmallRecordCount = 1_000;
    private const int MediumRecordCount = 10_000;
    private const int RepeatedWriteCount = 10_000;
    private const int RandomGetCount = 5_000;

    public static async Task<int> Main()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "BenchmarkData");
        ResetDirectory(root);

        Console.WriteLine("HerdKV benchmark");
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine();

        await RunPutBenchmark(root, SmallRecordCount);
        await RunPutBenchmark(root, MediumRecordCount);
        await RunHotGetBenchmark(root, verifyChecksumOnRead: true);
        await RunHotGetBenchmark(root, verifyChecksumOnRead: false);
        await RunRandomGetBenchmark(root, verifyChecksumOnRead: true);
        await RunRandomGetBenchmark(root, verifyChecksumOnRead: false);
        await RunStartupRecoveryBenchmark(root);
        await RunRepeatedWriteCompactionBenchmark(root);
        await RunBatchRepeatedWriteBenchmark(root);

        return 0;
    }

    private static async Task RunPutBenchmark(string root, int recordCount)
    {
        string path = Path.Combine(root, $"put-{recordCount}");
        ResetDirectory(path);

        byte[] value = Encoding.UTF8.GetBytes("benchmark-value-0123456789");

        await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);
        BenchmarkResult result = await MeasureAsync(async () =>
        {
            for (int i = 0; i < recordCount; i++)
            {
                await db.PutAsync($"key/{i:D8}", value);
            }

            await db.FlushAsync();
        });

        PrintResult($"Put {recordCount:N0}", recordCount, result);
        PrintStats(db.GetStats());
    }

    private static async Task RunHotGetBenchmark(string root, bool verifyChecksumOnRead)
    {
        string mode = verifyChecksumOnRead ? "safe" : "fast";
        string path = Path.Combine(root, $"get-hot-{mode}");
        ResetDirectory(path);

        await using IHerdKVStore db = await HerdKVStore.OpenAsync(path, CreateOptions(verifyChecksumOnRead));
        await db.PutAsync("hot", Encoding.UTF8.GetBytes("hot-value"));
        await db.FlushAsync();

        BenchmarkResult result = await MeasureAsync(async () =>
        {
            for (int i = 0; i < RandomGetCount; i++)
            {
                byte[]? value = await db.GetAsync("hot");
                if (value is null)
                {
                    throw new InvalidOperationException("Hot key was not found.");
                }
            }
        });

        PrintResult($"Get hot {mode} {RandomGetCount:N0}", RandomGetCount, result);
    }

    private static async Task RunRandomGetBenchmark(string root, bool verifyChecksumOnRead)
    {
        string mode = verifyChecksumOnRead ? "safe" : "fast";
        string path = Path.Combine(root, $"get-random-{mode}");
        ResetDirectory(path);

        await using IHerdKVStore db = await HerdKVStore.OpenAsync(path, CreateOptions(verifyChecksumOnRead));

        byte[] value = Encoding.UTF8.GetBytes("random-value");
        for (int i = 0; i < RandomGetCount; i++)
        {
            await db.PutAsync($"key/{i:D8}", value);
        }

        await db.FlushAsync();

        var random = new Random(1729);
        BenchmarkResult result = await MeasureAsync(async () =>
        {
            for (int i = 0; i < RandomGetCount; i++)
            {
                int keyIndex = random.Next(RandomGetCount);
                byte[]? found = await db.GetAsync($"key/{keyIndex:D8}");
                if (found is null)
                {
                    throw new InvalidOperationException("Random key was not found.");
                }
            }
        });

        PrintResult($"Get random {mode} {RandomGetCount:N0}", RandomGetCount, result);
    }

    private static async Task RunStartupRecoveryBenchmark(string root)
    {
        string path = Path.Combine(root, "startup-recovery");
        ResetDirectory(path);

        byte[] value = Encoding.UTF8.GetBytes("recovery-value");

        await using (IHerdKVStore db = await HerdKVStore.OpenAsync(path))
        {
            for (int i = 0; i < MediumRecordCount; i++)
            {
                await db.PutAsync($"key/{i:D8}", value);
            }
        }

        BenchmarkResult result = await MeasureAsync(async () =>
        {
            await using IHerdKVStore recovered = await HerdKVStore.OpenAsync(path);
            _ = recovered.GetStats();
        });

        PrintResult($"Startup recovery {MediumRecordCount:N0}", MediumRecordCount, result);
    }

    private static async Task RunRepeatedWriteCompactionBenchmark(string root)
    {
        string path = Path.Combine(root, "compaction");
        ResetDirectory(path);

        await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

        BenchmarkResult writeResult = await MeasureAsync(async () =>
        {
            for (int i = 0; i < RepeatedWriteCount; i++)
            {
                await db.PutAsync("hot/key", Encoding.UTF8.GetBytes($"value-{i:D8}"));
            }

            await db.FlushAsync();
        });

        HerdKVStats before = db.GetStats();
        BenchmarkResult compactResult = await MeasureAsync(async () => await db.CompactAsync());
        HerdKVStats after = db.GetStats();

        PrintResult($"Repeated write {RepeatedWriteCount:N0}", RepeatedWriteCount, writeResult);
        PrintResult("Compact hot key", 1, compactResult);
        Console.WriteLine($"  bytes before={before.TotalBytes:N0}, after={after.TotalBytes:N0}, dead after={after.DeadBytes:N0}");
        Console.WriteLine();
    }

    private static async Task RunBatchRepeatedWriteBenchmark(string root)
    {
        string path = Path.Combine(root, "batch-hot-key");
        ResetDirectory(path);

        await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);
        var operations = new List<HerdKVBatchOperation>(RepeatedWriteCount);
        for (int i = 0; i < RepeatedWriteCount; i++)
        {
            operations.Add(HerdKVBatchOperation.Put("hot/key", Encoding.UTF8.GetBytes($"value-{i:D8}")));
        }

        BenchmarkResult result = await MeasureAsync(async () =>
        {
            await db.WriteBatchAsync(operations);
            await db.FlushAsync();
        });

        HerdKVStats stats = db.GetStats();
        PrintResult($"Batch repeated write {RepeatedWriteCount:N0}", RepeatedWriteCount, result);
        PrintStats(stats);
    }

    private static async Task<BenchmarkResult> MeasureAsync(Func<Task> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long gen0Before = GC.CollectionCount(0);
        long gen1Before = GC.CollectionCount(1);
        long gen2Before = GC.CollectionCount(2);

        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();

        long allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);

        return new BenchmarkResult(
            stopwatch.Elapsed,
            allocatedAfter - allocatedBefore,
            GC.CollectionCount(0) - gen0Before,
            GC.CollectionCount(1) - gen1Before,
            GC.CollectionCount(2) - gen2Before);
    }

    private static void PrintResult(string name, int operations, BenchmarkResult result)
    {
        double seconds = Math.Max(result.Elapsed.TotalSeconds, 0.000001);
        Console.WriteLine($"{name}");
        Console.WriteLine($"  elapsed={result.Elapsed.TotalMilliseconds:N2} ms, ops/s={operations / seconds:N0}");
        Console.WriteLine($"  allocated={result.AllocatedBytes:N0} B, allocated/op={result.AllocatedBytes / Math.Max(operations, 1):N1} B");
        Console.WriteLine($"  GC gen0={result.Gen0Collections}, gen1={result.Gen1Collections}, gen2={result.Gen2Collections}");
    }

    private static void PrintStats(HerdKVStats stats)
    {
        Console.WriteLine($"  stats total={stats.TotalBytes:N0} B, live={stats.LiveBytes:N0} B, dead={stats.DeadBytes:N0} B, keys={stats.KeyCount:N0}, segments={stats.SegmentCount:N0}");
        Console.WriteLine();
    }

    private static HerdKVOptions CreateOptions(bool verifyChecksumOnRead)
    {
        return new HerdKVOptions
        {
            VerifyChecksumOnRead = verifyChecksumOnRead
        };
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private sealed record BenchmarkResult(
        TimeSpan Elapsed,
        long AllocatedBytes,
        long Gen0Collections,
        long Gen1Collections,
        long Gen2Collections);
}
