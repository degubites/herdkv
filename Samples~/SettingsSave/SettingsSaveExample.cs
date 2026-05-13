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
public sealed class SettingsSaveExample : MonoBehaviour
{
    private async void Start()
    {
        await using IHerdKVStore db = await HerdKVUnity.OpenAsync("settings-save");

        await db.PutAsync("settings/music", true, HerdKVCodecs.Boolean);
        await db.PutAsync("settings/sensitivity", 0.65f, HerdKVCodecs.Single);

        bool music = await db.GetRequiredAsync("settings/music", HerdKVCodecs.Boolean);
        float sensitivity = await db.GetRequiredAsync("settings/sensitivity", HerdKVCodecs.Single);
        Debug.Log($"Music: {music}, sensitivity: {sensitivity}");
    }
}
}
