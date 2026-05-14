# HerdKV 한국어 문서

HerdKV는 Unity 게임의 로컬 저장 데이터를 위한 임베디드 key-value 저장소입니다. 세이브 파일, 캐시, 월드 청크, 인벤토리, 설정값처럼 자주 읽고 부분적으로 갱신되는 데이터를 한 덩어리 JSON 파일보다 더 잘 다루는 것을 목표로 합니다.

## 설치

Unity Package Manager에서 git URL 또는 로컬 경로로 추가할 수 있습니다.

```text
https://codeberg.org/degubites/herdkv.git
```

로컬 개발 중이라면:

```text
Packages/com.degubites.herdkv
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

HerdKV는 게임 저장 데이터가 많은 독립 키로 나뉘는 경우에 잘 맞습니다.

좋은 사용처:

- 월드 청크를 좌표나 지역 키로 저장할 때
- 인벤토리 슬롯, 아이템 스택, 퀘스트 플래그, 진행 상태를 따로 저장할 때
- 로컬 설정, 플레이어 옵션, 기능 플래그를 저장할 때
- 생성 데이터, 썸네일, 다운로드 메타데이터 같은 캐시를 저장할 때
- 큰 세이브 파일 전체를 매번 다시 쓰고 싶지 않을 때
- crash recovery가 필요한 append-friendly 저장 구조가 필요할 때

잘 맞지 않는 사용처:

- SQL 같은 쿼리, 정렬, 조인, 리포팅
- Unity 오브젝트 그래프를 참조 단위로 직렬화
- 클라우드 저장, 동기화, 충돌 해결
- 여러 프로세스가 같은 DB 경로에 동시에 쓰기
- 여러 키를 하나의 트랜잭션으로 묶는 완전한 ACID DB

## 읽기 모드

기본값인 `VerifyChecksumOnRead=true`는 안전성 위주의 모드입니다. 값을 읽을 때 segment 파일에서 record를 다시 읽고 CRC를 검증합니다.

`VerifyChecksumOnRead=false`는 게임 실행 중 빠른 로컬 읽기에 더 잘 맞는 모드입니다. 열기와 쓰기 과정에서 값을 메모리에 캐시하고, 읽을 때는 메모리에서 복사본을 돌려줍니다. 시작 시 append-only log를 스캔하며 record 복구는 수행하지만, 개별 읽기마다 디스크 검증을 반복하지 않습니다.

```csharp
var options = new HerdKVOptions
{
    VerifyChecksumOnRead = false
};

await using var db = await HerdKVUnity.OpenAsync("main-save", options);
```

게임에서는 체크포인트, 씬 전환, 일시정지, 종료 시점에 `FlushAsync()`를 호출하는 식으로 사용하는 것을 권장합니다.

## 벤치마크 참고값

아래 결과는 한 머신에서 같은 조건으로 비교한 참고값입니다. 다른 PC, 빌드 타깃, 백그라운드 작업, SSD 상태에 따라 달라질 수 있습니다.

환경:

- CPU: AMD Ryzen 7 5700X3D
- RAM: 64 GB
- Storage: Samsung PM981a NVMe SSD
- OS: Windows
- Unity: 6000.0.65f1, Editor Play Mode
- Easy Save: JSON, encryption 없음, compression 없음
- HerdKV: `VerifyChecksumOnRead=false`

3회 반복 median:

| 시나리오 | JSON 통파일 | Easy Save | HerdKV |
|---|---:|---:|---:|
| 전체 오브젝트 저장+로드 | 20.1 ms | 45.9 ms | 56.0 ms |
| 많은 작은 키 쓰기+랜덤 읽기 | n/a | 6,404 ms | 2,963 ms |
| hot key 반복 갱신 | 10,750 ms | 2,811 ms | 5,872 ms |
| 청크 byte payload 쓰기+랜덤 읽기 | n/a | 7,546 ms | 428 ms |

해석:

- 작은 전체 세이브를 한 번에 저장하는 경우에는 JSON 통파일도 매우 경쟁력이 있습니다.
- 여러 키를 독립적으로 다루거나 월드 청크 같은 바이너리 payload를 저장할 때 HerdKV의 장점이 커집니다.
- 한 키만 매우 자주 덮어쓰는 hot-key 패턴은 append-only 구조상 old record가 쌓이므로 주기적인 `CompactAsync()`가 필요합니다.

## ACID 범위

HerdKV는 완전한 ACID 데이터베이스가 아닙니다.

현재 제공하는 것은 더 좁은 범위의 로컬 저장 보장입니다.

- 하나의 store 인스턴스 안에서 작업을 직렬화합니다.
- record는 append-only로 기록됩니다.
- 시작 시 segment를 스캔하고 incomplete/corrupt tail record를 무시하거나 잘라냅니다.
- `FlushAsync()` 이후에는 active writer를 파일 시스템으로 밀어냅니다.

현재 제공하지 않는 것:

- 여러 키를 묶는 transaction
- 여러 프로세스/여러 store 인스턴스의 동시 쓰기 보장
- OS 전원 차단까지 강하게 보장하는 fsync/write-through 모드
- SQL 쿼리 엔진

## 더 보기

- [Getting Started](manual/getting-started.md)
- [Unity Installation](manual/unity-installation.md)
- [Performance](manual/performance.md)
- [Benchmarks](manual/benchmarks.md)
- [Limitations](manual/limitations.md)
- [Samples](manual/samples.md)
