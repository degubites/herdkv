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
public sealed class HerdKVAutoFlush : MonoBehaviour
{
    public IHerdKVStore? Store { get; set; }

    private async void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            await FlushAsync();
        }
    }

    private async void OnApplicationQuit()
    {
        await FlushAsync();
    }

    private async ValueTask FlushAsync()
    {
        if (Store is null)
        {
            return;
        }

        try
        {
            await Store.FlushAsync();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
}
