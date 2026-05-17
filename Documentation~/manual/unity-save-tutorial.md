# Unity Save Tutorial

This tutorial shows a practical Unity save flow with HerdKV:

- primitive settings
- JSON DTO saves
- prefix key listing
- batch writes
- explicit flush points

The example is shaped like a small extraction RPG save system, but the same pattern works for inventories, quest flags, local caches, and sync outboxes.

## 1. Define Save DTOs

HerdKV does not own JSON serialization. Pick the serializer that fits your project, then store the serialized `string` or `byte[]`.

The examples below use `JsonUtility` because it is built into Unity. If your project uses Newtonsoft.Json, the HerdKV calls stay the same and only the serialization lines change.

```csharp
using System;

[Serializable]
public sealed class EquipmentSave
{
    public string id;
    public string slot;
    public string rarity;
    public int durability;
    public int maxDurability;
}

[Serializable]
public sealed class RunStateSave
{
    public string runId;
    public int depth;
    public int fuelRemaining;
    public int backpackValue;
    public bool exitPointExists;
}

[Serializable]
public sealed class SyncEventSave
{
    public string eventId;
    public string eventType;
    public string payloadKey;
}
```

## 2. Open a Store

Use `HerdKVUnity.OpenAsync` to create a database under `Application.persistentDataPath/HerdKV`.

```csharp
var options = new HerdKVOptions
{
    VerifyChecksumOnRead = false,
    FlushMode = HerdKVFlushMode.Manual
};

await using IHerdKVStore db = await HerdKVUnity.OpenAsync("main-save", options);
```

`VerifyChecksumOnRead=false` enables fast local reads from the in-memory value cache. Startup recovery still validates the append-only log while opening the database.

`Manual` mode is the recommended default for gameplay. Call `FlushAsync()` at clear save points.

## 3. Save Primitive Settings

Use built-in codecs for small primitive values.

```csharp
await db.PutAsync("settings/autoExtractHp", 35, HerdKVCodecs.Int32);
await db.PutAsync("settings/challengerAvoid", true, HerdKVCodecs.Boolean);
await db.PutAsync("settings/aggression", 0.65f, HerdKVCodecs.Single);
```

Read them back with the same codec.

```csharp
int autoExtractHp = await db.GetRequiredAsync("settings/autoExtractHp", HerdKVCodecs.Int32);
bool challengerAvoid = await db.GetRequiredAsync("settings/challengerAvoid", HerdKVCodecs.Boolean);
```

## 4. Save DTOs as JSON

Serialize the DTO to a JSON string, then store it with the UTF-8 string codec.

```csharp
var sword = new EquipmentSave
{
    id = "sword_001",
    slot = "weapon",
    rarity = "rare",
    durability = 42,
    maxDurability = 50
};

string swordJson = JsonUtility.ToJson(sword);
await db.PutAsync($"equipment/{sword.id}", swordJson, HerdKVCodecs.StringUtf8);
```

Load the DTO:

```csharp
string loadedJson = await db.GetRequiredAsync("equipment/sword_001", HerdKVCodecs.StringUtf8);
EquipmentSave loaded = JsonUtility.FromJson<EquipmentSave>(loadedJson);
```

If the key may be missing, use `GetAsync` and handle `null`:

```csharp
string? missingJson = await db.GetAsync("equipment/missing", HerdKVCodecs.StringUtf8);
EquipmentSave? missing = missingJson is null
    ? null
    : JsonUtility.FromJson<EquipmentSave>(missingJson);
```

The same pattern works with Newtonsoft.Json if you already use it in your game:

```csharp
// using Newtonsoft.Json;

string json = JsonConvert.SerializeObject(sword);
await db.PutAsync($"equipment/{sword.id}", json, HerdKVCodecs.StringUtf8);

string savedJson = await db.GetRequiredAsync("equipment/sword_001", HerdKVCodecs.StringUtf8);
EquipmentSave savedSword = JsonConvert.DeserializeObject<EquipmentSave>(savedJson)!;
```

## 5. List a Prefix

Use `ListKeysAsync(prefix)` to list live keys in ordinal sorted order.

```csharp
IReadOnlyList<string> equipmentKeys = await db.ListKeysAsync("equipment/");

foreach (string key in equipmentKeys)
{
    string json = await db.GetRequiredAsync(key, HerdKVCodecs.StringUtf8);
    EquipmentSave item = JsonUtility.FromJson<EquipmentSave>(json);
    Debug.Log($"{item.id}: {item.rarity} {item.durability}/{item.maxDurability}");
}
```

Prefix listing is useful for:

- `equipment/{equipId}`
- `characters/{charId}`
- `runs/current/rooms/{roomIndex}`
- `sync/outbox/{eventId}`

For value-based queries such as rarity, location, or equipped character, maintain explicit secondary index keys.

Example:

```text
equipment/sword_001
indexes/equipment/rarity/rare/sword_001
indexes/equipment/location/warehouse/sword_001
```

## 6. Write Related Changes in a Batch

Use `WriteBatchAsync` when several changes belong to one save step. Repeated keys are coalesced before records are appended.

```csharp
var runState = new RunStateSave
{
    runId = "run_001",
    depth = 4,
    fuelRemaining = 52,
    backpackValue = 1200,
    exitPointExists = false
};

string syncEventId = Guid.NewGuid().ToString("N");
var syncEvent = new SyncEventSave
{
    eventId = syncEventId,
    eventType = "run_checkpoint",
    payloadKey = "runs/current/state"
};

await db.WriteBatchAsync(new[]
{
    HerdKVBatchOperation.Put("runs/current/state", JsonUtility.ToJson(runState), HerdKVCodecs.StringUtf8),
    HerdKVBatchOperation.Put($"sync/outbox/{syncEventId}", JsonUtility.ToJson(syncEvent), HerdKVCodecs.StringUtf8),
    HerdKVBatchOperation.Put("player/fuel", runState.fuelRemaining, HerdKVCodecs.Int32)
});
```

Batch writes are a performance feature, not multi-key ACID transactions. See [Durability](durability.md) for the exact save-point policy.

## 7. Flush at Save Points

`FlushAsync()` is HerdKV's application-level durability boundary.

Call it after important save steps:

```csharp
await db.FlushAsync();
```

Recommended Unity points:

- explicit player save
- checkpoint completion
- room completion
- before scene transition
- application pause
- application quit

`HerdKVAutoFlush` can flush a store on pause and quit if you keep a long-lived store open.

## Full MonoBehaviour Example

```csharp
using System;
using System.Collections.Generic;
using Degubites.HerdKV;
using UnityEngine;

public sealed class HerdKVSaveTutorial : MonoBehaviour
{
    private async void Start()
    {
        var options = new HerdKVOptions
        {
            VerifyChecksumOnRead = false,
            FlushMode = HerdKVFlushMode.Manual
        };

        await using IHerdKVStore db = await HerdKVUnity.OpenAsync("tutorial-save", options);

        await db.PutAsync("settings/autoExtractHp", 35, HerdKVCodecs.Int32);
        await db.PutAsync("settings/challengerAvoid", true, HerdKVCodecs.Boolean);

        var sword = new EquipmentSave
        {
            id = "sword_001",
            slot = "weapon",
            rarity = "rare",
            durability = 42,
            maxDurability = 50
        };

        string swordJson = JsonUtility.ToJson(sword);
        await db.PutAsync($"equipment/{sword.id}", swordJson, HerdKVCodecs.StringUtf8);

        IReadOnlyList<string> equipmentKeys = await db.ListKeysAsync("equipment/");
        foreach (string key in equipmentKeys)
        {
            string json = await db.GetRequiredAsync(key, HerdKVCodecs.StringUtf8);
            EquipmentSave item = JsonUtility.FromJson<EquipmentSave>(json);
            Debug.Log($"{key}: {item.rarity} {item.durability}/{item.maxDurability}");
        }

        await db.FlushAsync();
    }
}
```

## Serializer Notes

HerdKV stores bytes. JSON, MessagePack, MemoryPack, and custom binary formats should be handled before calling `PutAsync`.

For `JsonUtility`, keep these constraints in mind.

Good fit:

- simple save DTO classes
- public fields
- arrays and nested serializable classes
- Unity-friendly save data

Limitations:

- dictionary support is limited
- polymorphism is limited
- property-only DTOs are not a good fit
- private setters are not a good fit

For Newtonsoft.Json, System.Text.Json, MessagePack, MemoryPack, or a custom binary format, serialize to `string` or `byte[]` yourself and store the payload with HerdKV codecs.
