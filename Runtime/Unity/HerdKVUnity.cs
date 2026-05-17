using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;

namespace Degubites.HerdKV
{
public static class HerdKVUnity
{
    public static ValueTask<IHerdKVStore> OpenAsync(
        string databaseName,
        HerdKVOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return HerdKVStore.OpenAsync(GetDatabasePath(databaseName), options, cancellationToken);
    }

    public static ValueTask<IHerdKVStore> OpenAsync(
        string databaseName,
        HerdKVOpenOptions options,
        CancellationToken cancellationToken = default)
    {
        return HerdKVStore.OpenAsync(GetDatabasePath(databaseName), options, cancellationToken);
    }

    public static ValueTask<IHerdKVStore> OpenAsync(
        HerdKVSettings settings,
        CancellationToken cancellationToken = default)
    {
        return HerdKVStore.OpenAsync(GetDatabasePath(settings.DatabaseName), settings.ToOpenOptions(), cancellationToken);
    }

    public static ValueTask<IHerdKVStore> OpenAsync(
        HerdKVSettings settings,
        HerdKVOpenOptions openOptions,
        CancellationToken cancellationToken = default)
    {
        HerdKVOpenOptions merged = settings.ToOpenOptions();
        merged.SchemaVersion = openOptions.SchemaVersion;
        merged.CreateBackupBeforeMigration = openOptions.CreateBackupBeforeMigration;
        foreach (IHerdKVMigration migration in openOptions.Migrations)
        {
            merged.Migrations.Add(migration);
        }

        return HerdKVStore.OpenAsync(GetDatabasePath(settings.DatabaseName), merged, cancellationToken);
    }

    public static ValueTask<IHerdKVStore> OpenSlotAsync(
        string slotName,
        HerdKVOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return HerdKVStore.OpenAsync(GetSlotPath(slotName), options, cancellationToken);
    }

    public static ValueTask<IHerdKVStore> OpenSlotAsync(
        string slotName,
        HerdKVOpenOptions options,
        CancellationToken cancellationToken = default)
    {
        return HerdKVStore.OpenAsync(GetSlotPath(slotName), options, cancellationToken);
    }

    public static string GetDatabasePath(string databaseName)
    {
        return Path.Combine(Application.persistentDataPath, "HerdKV", SanitizePathPart(databaseName));
    }

    public static string GetSlotPath(string slotName)
    {
        return Path.Combine(Application.persistentDataPath, "HerdKV", "Slots", SanitizePathPart(slotName));
    }

    private static string SanitizePathPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Database or slot name must not be empty.", nameof(value));
        }

        var builder = new StringBuilder(value.Length);
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in value)
        {
            builder.Append(invalid.Contains(c) || c == '/' || c == '\\' ? '_' : c);
        }

        string sanitized = builder.ToString().Trim();
        if (sanitized.Length == 0 || sanitized == "." || sanitized == "..")
        {
            throw new ArgumentException("Database or slot name must not resolve to the current or parent directory.", nameof(value));
        }

        return sanitized;
    }
}
}
