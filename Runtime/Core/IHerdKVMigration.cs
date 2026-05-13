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
public interface IHerdKVMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    ValueTask MigrateAsync(IHerdKVStore db);
}
}
