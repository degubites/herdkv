using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Degubites.HerdKV.Samples
{
public sealed class MigrationExample : MonoBehaviour
{
    private async void Start()
    {
        HerdKVOpenOptions options = new() { SchemaVersion = 1 };
        options.Migrations.Add(new AddLevelMigration());

        await using IHerdKVStore db = await HerdKVUnity.OpenAsync("migration-save", options);
        int level = await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32);
        Debug.Log($"Migrated level: {level}");
    }

    private sealed class AddLevelMigration : IHerdKVMigration
    {
        public int FromVersion => 0;

        public int ToVersion => 1;

        public ValueTask MigrateAsync(IHerdKVStore db)
        {
            return db.PutAsync("player/level", 1, HerdKVCodecs.Int32);
        }
    }
}
}
