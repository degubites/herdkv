using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
namespace Degubites.HerdKV
{
internal static class HerdKVMigrationRunner
{
    public static async ValueTask RunAsync(
        string path,
        IHerdKVStore store,
        HerdKVOpenOptions options,
        CancellationToken cancellationToken)
    {
        int currentVersion = await HerdKVSchema.GetVersionAsync(store, cancellationToken);
        int targetVersion = options.SchemaVersion;

        if (targetVersion < currentVersion)
        {
            throw new HerdKVMigrationException($"Database schema version {currentVersion} is newer than requested version {targetVersion}.");
        }

        if (targetVersion == currentVersion)
        {
            return;
        }

        for (int version = currentVersion; version < targetVersion; version++)
        {
            if (!options.Migrations.Any(migration => migration.FromVersion == version && migration.ToVersion == version + 1))
            {
                throw new HerdKVMigrationException($"Missing migration from schema version {version} to {version + 1}.");
            }
        }

        string? backupPath = null;
        if (options.CreateBackupBeforeMigration && Directory.Exists(path))
        {
            backupPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".migration-backup-" + Guid.NewGuid().ToString("N");
            CopyDirectory(path, backupPath);
        }

        try
        {
            for (int version = currentVersion; version < targetVersion; version++)
            {
                IHerdKVMigration migration = options.Migrations.First(m => m.FromVersion == version && m.ToVersion == version + 1);
                await migration.MigrateAsync(store);
                await HerdKVSchema.SetVersionAsync(store, version + 1, cancellationToken);
            }

            await store.FlushAsync(cancellationToken);

            if (backupPath is not null && Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            if (backupPath is not null && Directory.Exists(backupPath))
            {
                await store.DisposeAsync();
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                Directory.Move(backupPath, path);
            }

            throw new HerdKVMigrationException("HerdKV migration failed and the database was restored from backup.", ex);
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (string directory in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (string file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourcePath, file);
            File.Copy(file, Path.Combine(destinationPath, relativePath), overwrite: true);
        }
    }
}
}
