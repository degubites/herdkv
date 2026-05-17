# Unity 저장 튜토리얼

이 튜토리얼은 HerdKV로 Unity 게임 저장 흐름을 구성하는 방법을 보여줍니다.

- primitive 설정값 저장
- JSON DTO 저장
- prefix key listing
- batch write
- 명시적인 flush 시점

예시는 익스트랙션 RPG 저장 구조에 가깝게 잡았지만, 인벤토리, 퀘스트 플래그, 로컬 캐시, sync outbox에도 같은 패턴을 사용할 수 있습니다.

## 1. 저장 DTO 정의

HerdKV는 JSON 직렬화를 직접 맡지 않습니다. 프로젝트에 맞는 직렬화 라이브러리를 고른 뒤, 직렬화된 `string` 또는 `byte[]`를 저장하면 됩니다.

아래 예시는 Unity에 기본으로 들어있는 `JsonUtility`를 사용합니다. 프로젝트에서 Newtonsoft.Json을 쓴다면 HerdKV 호출은 그대로 두고 직렬화 코드만 바꾸면 됩니다.

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

## 2. Store 열기

`HerdKVUnity.OpenAsync`를 사용하면 `Application.persistentDataPath/HerdKV` 아래에 데이터베이스가 생성됩니다.

```csharp
var options = new HerdKVOptions
{
    VerifyChecksumOnRead = false,
    FlushMode = HerdKVFlushMode.Manual
};

await using IHerdKVStore db = await HerdKVUnity.OpenAsync("main-save", options);
```

`VerifyChecksumOnRead=false`는 로컬 게임플레이 중 빠른 읽기에 적합합니다. 데이터베이스를 열 때 append-only log를 검증하며 복구하고, 개별 읽기는 메모리 캐시에서 처리합니다.

`Manual` 모드는 일반적인 게임 저장에 추천되는 기본 모드입니다. 중요한 저장 지점에서 `FlushAsync()`를 호출하세요.

## 3. Primitive 설정값 저장

작은 설정값은 built-in codec을 사용하는 것이 편합니다.

```csharp
await db.PutAsync("settings/autoExtractHp", 35, HerdKVCodecs.Int32);
await db.PutAsync("settings/challengerAvoid", true, HerdKVCodecs.Boolean);
await db.PutAsync("settings/aggression", 0.65f, HerdKVCodecs.Single);
```

읽을 때도 같은 codec을 사용합니다.

```csharp
int autoExtractHp = await db.GetRequiredAsync("settings/autoExtractHp", HerdKVCodecs.Int32);
bool challengerAvoid = await db.GetRequiredAsync("settings/challengerAvoid", HerdKVCodecs.Boolean);
```

## 4. DTO를 JSON으로 저장

DTO를 JSON 문자열로 직렬화한 뒤 UTF-8 string codec으로 저장합니다.

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

읽을 때:

```csharp
string loadedJson = await db.GetRequiredAsync("equipment/sword_001", HerdKVCodecs.StringUtf8);
EquipmentSave loaded = JsonUtility.FromJson<EquipmentSave>(loadedJson);
```

키가 없을 수 있다면 `GetAsync`를 사용하고 `null`을 처리합니다.

```csharp
string? missingJson = await db.GetAsync("equipment/missing", HerdKVCodecs.StringUtf8);
EquipmentSave? missing = missingJson is null
    ? null
    : JsonUtility.FromJson<EquipmentSave>(missingJson);
```

이미 Newtonsoft.Json을 쓰는 프로젝트라면 같은 패턴으로 처리하면 됩니다.

```csharp
// using Newtonsoft.Json;

string json = JsonConvert.SerializeObject(sword);
await db.PutAsync($"equipment/{sword.id}", json, HerdKVCodecs.StringUtf8);

string savedJson = await db.GetRequiredAsync("equipment/sword_001", HerdKVCodecs.StringUtf8);
EquipmentSave savedSword = JsonConvert.DeserializeObject<EquipmentSave>(savedJson)!;
```

## 5. Prefix로 key 목록 읽기

`ListKeysAsync(prefix)`는 live key 목록을 ordinal 정렬 순서로 반환합니다.

```csharp
IReadOnlyList<string> equipmentKeys = await db.ListKeysAsync("equipment/");

foreach (string key in equipmentKeys)
{
    string json = await db.GetRequiredAsync(key, HerdKVCodecs.StringUtf8);
    EquipmentSave item = JsonUtility.FromJson<EquipmentSave>(json);
    Debug.Log($"{item.id}: {item.rarity} {item.durability}/{item.maxDurability}");
}
```

이런 key 구조에 잘 맞습니다.

- `equipment/{equipId}`
- `characters/{charId}`
- `runs/current/rooms/{roomIndex}`
- `sync/outbox/{eventId}`

희귀도, 위치, 장착 캐릭터처럼 value 기반으로 검색해야 한다면 직접 secondary index key를 관리하는 편이 좋습니다.

예:

```text
equipment/sword_001
indexes/equipment/rarity/rare/sword_001
indexes/equipment/location/warehouse/sword_001
```

## 6. 관련 변경을 batch로 저장

한 저장 단계에서 여러 값을 바꾼다면 `WriteBatchAsync`를 사용하세요. batch 안에서 같은 key가 여러 번 나오면 append 전에 마지막 작업만 남도록 coalesce됩니다.

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

Batch write는 성능 기능이지 multi-key ACID transaction은 아닙니다. 저장 완료 기준은 [Durability](durability.md) 문서를 참고하세요.

## 7. 저장 지점에서 Flush

`FlushAsync()`는 HerdKV의 앱 레벨 durability boundary입니다.

중요한 저장 단계 뒤에 호출하세요.

```csharp
await db.FlushAsync();
```

추천 시점:

- 유저가 명시적으로 저장했을 때
- 체크포인트 완료
- 방 클리어
- 씬 전환 전
- application pause
- application quit

store를 길게 열어두는 구조라면 `HerdKVAutoFlush`로 pause/quit 시점 flush를 맡길 수 있습니다.

## 전체 MonoBehaviour 예시

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

## 직렬화 주의사항

HerdKV는 byte를 저장합니다. JSON, MessagePack, MemoryPack, 직접 만든 binary format은 `PutAsync`를 호출하기 전에 먼저 직렬화하면 됩니다.

`JsonUtility`를 사용할 때는 아래 제약을 염두에 두세요.

잘 맞는 경우:

- 단순 저장 DTO class
- public field
- 배열과 중첩 serializable class
- Unity 친화적인 save data

주의할 점:

- Dictionary 지원이 제한적입니다.
- polymorphism 지원이 제한적입니다.
- property-only DTO와 잘 맞지 않습니다.
- private setter 위주의 C# DTO와 잘 맞지 않습니다.

Newtonsoft.Json, System.Text.Json, MessagePack, MemoryPack, 직접 만든 binary format을 쓰는 경우에도 직접 `string` 또는 `byte[]`로 직렬화한 뒤 HerdKV codec으로 저장하세요.
