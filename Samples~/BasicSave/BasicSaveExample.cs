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
public sealed class BasicSaveExample : MonoBehaviour
{
    private async void Start()
    {
        await using IHerdKVStore db = await HerdKVUnity.OpenAsync("basic-save");

        await db.PutAsync("player/name", "Alice", HerdKVCodecs.StringUtf8);
        int launches = await db.GetAsync("app/launches", HerdKVCodecs.Int32) ?? 0;
        await db.PutAsync("app/launches", launches + 1, HerdKVCodecs.Int32);
        await db.FlushAsync();

        Debug.Log($"Saved Alice. Launches: {launches + 1}");
    }
}
}
