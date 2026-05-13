using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;

namespace Degubites.HerdKV.Samples
{
public sealed class WorldChunkSaveExample : MonoBehaviour
{
    private async void Start()
    {
        await using IHerdKVStore db = await HerdKVUnity.OpenAsync("world-save");

        string key = GetChunkKey(12, -4);
        await db.PutAsync(key, Encoding.UTF8.GetBytes("chunk payload"));

        byte[]? payload = await db.GetAsync(key);
        Debug.Log($"Loaded chunk bytes: {payload?.Length ?? 0}");
    }

    private static string GetChunkKey(int x, int y)
    {
        return $"chunks/{x}/{y}";
    }
}
}
