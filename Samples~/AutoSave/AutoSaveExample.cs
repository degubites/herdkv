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
public sealed class AutoSaveExample : MonoBehaviour
{
    private IHerdKVStore? db;

    private async void Start()
    {
        db = await HerdKVUnity.OpenAsync("auto-save");
        await db.PutAsync("session/started", true, HerdKVCodecs.Boolean);

        HerdKVAutoFlush autoFlush = gameObject.AddComponent<HerdKVAutoFlush>();
        autoFlush.Store = db;
    }

    private async void OnDestroy()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }
    }
}
}
