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
public sealed class InventorySaveExample : MonoBehaviour
{
    private async void Start()
    {
        await using IHerdKVStore db = await HerdKVUnity.OpenAsync("inventory-save");

        await db.PutAsync("inventory/potion", 5, HerdKVCodecs.Int32);
        await db.PutAsync("inventory/key", 1, HerdKVCodecs.Int32);

        int potions = await db.GetRequiredAsync("inventory/potion", HerdKVCodecs.Int32);
        Debug.Log($"Potions: {potions}");
    }
}
}
