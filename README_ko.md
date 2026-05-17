# HerdKV

HerdKV는 Unity 게임의 저장 데이터, 캐시, 자주 갱신되는 로컬 게임 상태를 위한 임베디드 key-value 저장소입니다.

English README: [README.md](README.md)

튜토리얼: [Unity 저장 튜토리얼](Documentation~/manual/unity-save-tutorial.ko.md) / [Unity Save Tutorial](Documentation~/manual/unity-save-tutorial.md)

이슈와 기능 요청은 Codeberg에서만 받습니다: [codeberg.org/degubites/herdkv/issues](https://codeberg.org/degubites/herdkv/issues). GitHub는 OpenUPM 배포를 위한 읽기 전용 미러로 사용합니다.

## 설치

Unity Package Manager에서 git URL 또는 로컬 경로로 추가할 수 있습니다.

```text
https://codeberg.org/degubites/herdkv.git
```

로컬 개발 중이라면:

```text
Packages/com.degubites.herdkv
```

특정 릴리즈를 고정하고 싶다면 tag를 붙입니다.

```text
https://codeberg.org/degubites/herdkv.git#v0.10.0
```

## 빠른 예시

```csharp
await using var db = await HerdKVUnity.OpenAsync("main-save");

await db.PutAsync("player/name", "Alice", HerdKVCodecs.StringUtf8);
await db.PutAsync("player/level", 42, HerdKVCodecs.Int32);

string name = await db.GetRequiredAsync("player/name", HerdKVCodecs.StringUtf8);
int level = await db.GetRequiredAsync("player/level", HerdKVCodecs.Int32);

await db.FlushAsync();
```

`HerdKVUnity`로 연 데이터베이스는 `Application.persistentDataPath/HerdKV` 아래에 만들어집니다.

## 언제 쓰면 좋은가

HerdKV는 큰 JSON 저장 파일 하나를 매번 다시 쓰기보다, 여러 독립적인 값을 key 단위로 갱신하고 싶을 때 잘 맞습니다.

좋은 사용처:

- 월드 청크, 지역, 방 상태를 좌표나 ID별 key로 저장
- 인벤토리 슬롯, 아이템 스택, 퀘스트 플래그, 진행 상태 저장
- 로컬 설정, 플레이어 옵션, 기능 플래그 저장
- 생성 데이터, 썸네일, 다운로드 메타데이터 같은 로컬 캐시 저장
- SQL 쿼리보다 append-only 저장과 crash recovery가 더 중요한 데이터

잘 맞지 않는 사용처:

- SQL 같은 쿼리, 정렬, 조인, 리포트
- Unity 오브젝트 그래프를 참조 단위로 직렬화
- 클라우드 저장, 동기화 충돌 해결
- 여러 프로세스가 같은 DB 경로에 동시에 접근
- 여러 key를 하나의 완전한 ACID transaction으로 묶는 저장소

## JSON과 직렬화

HerdKV는 객체 직렬화 프레임워크가 아니라 byte 저장소입니다. DTO나 클래스를 저장할 때는 프로젝트에서 쓰는 직렬화 도구로 `string` 또는 `byte[]`를 만든 뒤 `PutAsync`로 저장합니다.

Unity `JsonUtility` 예시:

```csharp
string json = JsonUtility.ToJson(item);
await db.PutAsync($"equipment/{item.id}", json, HerdKVCodecs.StringUtf8);
```

Newtonsoft.Json 예시:

```csharp
string json = JsonConvert.SerializeObject(item);
await db.PutAsync($"equipment/{item.id}", json, HerdKVCodecs.StringUtf8);
```

자세한 흐름은 [Unity 저장 튜토리얼](Documentation~/manual/unity-save-tutorial.ko.md)을 참고하세요.

## Prefix key listing

`ListKeysAsync(prefix)`로 특정 prefix를 가진 live key 목록을 정렬된 상태로 가져올 수 있습니다.

```csharp
IReadOnlyList<string> equipmentKeys = await db.ListKeysAsync("equipment/");
IReadOnlyList<string> outboxKeys = await db.ListKeysAsync("sync/outbox/");
```

희귀도, 위치, 장착 캐릭터처럼 value 기반으로 검색해야 한다면 별도의 secondary index key를 직접 관리하는 편이 좋습니다.

## Batch write와 flush

한 저장 단계에서 여러 값을 바꾼다면 `WriteBatchAsync`를 사용할 수 있습니다. batch 안에서 같은 key가 여러 번 나오면 마지막 작업만 실제 record로 기록됩니다.

```csharp
await db.WriteBatchAsync(new[]
{
    HerdKVBatchOperation.Put("player/gold", 250, HerdKVCodecs.Int32),
    HerdKVBatchOperation.Put("quest/intro", true, HerdKVCodecs.Boolean),
    HerdKVBatchOperation.Delete("cache/old-thumbnail")
});
```

HerdKV에서 일반적인 저장 완료 기준은 `FlushAsync()`입니다. 체크포인트, 씬 전환, 일시정지, 종료 같은 명확한 save point에서 호출하는 것을 권장합니다.

## 문서

- [한국어 패키지 문서](Documentation~/README.ko.md)
- [Unity 저장 튜토리얼](Documentation~/manual/unity-save-tutorial.ko.md)
- [Unity 설치](Documentation~/manual/unity-installation.md)
- [Public API](Documentation~/manual/public-api.md)
- [Durability](Documentation~/manual/durability.md)
- [Benchmarks](Documentation~/manual/benchmarks.md)
- [Samples](Documentation~/manual/samples.md)
- [Limitations](Documentation~/manual/limitations.md)
- [FAQ](Documentation~/manual/faq.md)
