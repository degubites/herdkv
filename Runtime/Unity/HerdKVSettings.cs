using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Degubites.HerdKV
{
[CreateAssetMenu(menuName = "HerdKV/Settings", fileName = "HerdKVSettings")]
public sealed class HerdKVSettings : ScriptableObject
{
    [SerializeField] private string databaseName = "main-save";
    [SerializeField] private long segmentSizeBytes = 4 * 1024 * 1024;
    [SerializeField] private bool verifyChecksumOnRead = true;
    [SerializeField] private HerdKVFlushMode flushMode = HerdKVFlushMode.Manual;
    [SerializeField] private int schemaVersion;
    [SerializeField] private bool createBackupBeforeMigration = true;

    public string DatabaseName => databaseName;

    public HerdKVOptions ToCoreOptions()
    {
        return new HerdKVOptions
        {
            SegmentSizeBytes = segmentSizeBytes,
            VerifyChecksumOnRead = verifyChecksumOnRead,
            FlushMode = flushMode
        };
    }

    public HerdKVOpenOptions ToOpenOptions()
    {
        HerdKVOpenOptions options = new()
        {
            SchemaVersion = schemaVersion,
            CreateBackupBeforeMigration = createBackupBeforeMigration
        };

        options.StorageOptions.SegmentSizeBytes = segmentSizeBytes;
        options.StorageOptions.VerifyChecksumOnRead = verifyChecksumOnRead;
        options.StorageOptions.FlushMode = flushMode;
        return options;
    }
}
}
