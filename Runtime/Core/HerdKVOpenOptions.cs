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
public sealed class HerdKVOpenOptions
{
    public HerdKVOptions StorageOptions { get; } = new();

    public int SchemaVersion { get; set; }

    public bool CreateBackupBeforeMigration { get; set; } = true;

    public List<IHerdKVMigration> Migrations { get; } = new();
}
}
