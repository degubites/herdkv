using Degubites.HerdKV;

string path = Path.Combine(AppContext.BaseDirectory, "SampleData", "main-save");

await using IHerdKVStore db = await HerdKVStore.OpenAsync(path);

await db.PutAsync("player/name", "Alice", HerdKVCodecs.StringUtf8);
await db.PutAsync("player/level", 7, HerdKVCodecs.Int32);
await db.FlushAsync();

string? name = await db.GetAsync("player/name", HerdKVCodecs.StringUtf8);
int level = await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32);

Console.WriteLine($"Player: {name}, level {level}");
Console.WriteLine($"Database: {path}");

HerdKVStats stats = db.GetStats();
Console.WriteLine($"Keys: {stats.KeyCount}, bytes: {stats.TotalBytes}");
