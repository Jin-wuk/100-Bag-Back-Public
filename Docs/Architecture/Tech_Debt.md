# 기술 부채 (Tech Debt)

'선(先) 기능 구현, 후(後) 리팩토링' 원칙에 따라 당장의 속도를 위해 타협했고, 나중에 고쳐야 하는 항목입니다.
각 항목은 **트리거 시점**이 오면 처리합니다. 처리하면 이 문서에서 지웁니다.

> 2026-11-03 코드 기준으로 확인한 목록입니다. 이전 `04_Refactoring_TODO.md`와 `05_Implementation_Gap_Analysis.md` 4절(B1~B13)을 합쳤고, 이미 고친 B1·B2·B8~B12는 뺐습니다.

## 1. 구조

| 항목 | 현재 상태 | 개선 방향 | 트리거 시점 |
| --- | --- | --- | --- |
| **UI가 씬을 직접 전환** | `DungeonEntranceUI`가 `SceneManager.LoadScene`을 직접 호출. `DungeonExit`, `PlayerStatManager`(사망)도 각자 씬을 바꿈 | UI는 `GlobalEvents.OnDungeonLoadRequested` 같은 커맨드 이벤트만 던지고, 씬 전환 담당(예: `SceneLoadManager`)이 트랜지션 → 비동기 로드 → 던전 생성을 순서대로 처리 | 던전 진입 연출이나 비동기 로드 작업 시작 시 |
| **`FindObjectOfType` 런타임 탐색** | `HubSystem`, `HubTradeSystem`, 인벤토리·창고·파우치·퀵슬롯 UI, `MonsterController`, `PlayerInput` 등이 초기화 때 씬 전체를 탐색 (대부분 `Awake`/`Start`, 일부는 `InventoryUIController`의 우클릭 처리처럼 입력 시점) | 인스펙터 참조나 매니저 생성 이벤트(`OnManagerCreated`)로 주입. 입력 시점 탐색부터 우선 제거 | 씬 오브젝트가 늘어 초기화가 느려지거나, 같은 타입이 둘 이상 생길 때 |
| **몬스터 특성을 문자열로 비교** | `MonsterDataSO.HasTrait("FAST")` 등 매직 스트링 | `MonsterTrait` enum(또는 플래그)으로 전환. 에셋의 `traits` 값 마이그레이션 필요 | 특성 AI 반응([Roadmap](../GDD/Roadmap.md) 3절) 작업 시 |

## 2. 성능

| 항목 | 현재 상태 | 개선 방향 | 트리거 시점 |
| --- | --- | --- | --- |
| **바닥 아이템 풀링** | `LootSystem`이 바닥 아이템(`LootableItem`)을 `new GameObject`로 만들고 주우면 파괴 (`// TODO: 풀링`) | `CombatFeedbackSystem`의 프리팹별 큐 풀과 같은 방식으로 풀을 만들고, 줍기 시 비활성화 후 반환 | 드랍량이 많은 몬스터·상자·보스 작업 시 |
| **드랍 데이터 전수 로드** | `LootSystem` 초기화 때 `Resources.LoadAll<DropPoolSO>`·`LoadAll<ItemDataSO>`로 전부 읽음 | 지역 설정(`RegionConfigSO`)이 쓰는 풀만 참조로 들고 있게 하거나, ID → 에셋 테이블 SO 하나로 대체 | 아이템·드랍 풀이 지역별로 늘어날 때 |
| **피격마다 컴포넌트 탐색** | `PlayerStatManager.TakeDamage`가 맞을 때마다 `GetComponentInChildren<SpriteRenderer>()` | `Awake`에서 캐싱 | 다음에 `PlayerStatManager`를 수정할 때 같이 |
| **레이어 문자열 조회** | `LootSystem`, `Projectile`, `PlayerMovement`가 `LayerMask.GetMask`·`NameToLayer`를 문자열로 호출 | 인스펙터 `LayerMask` 필드나 `static readonly` 캐시로 대체 | 다음에 해당 스크립트를 수정할 때 같이 |

## 3. 데이터 · 도구

| 항목 | 현재 상태 | 개선 방향 | 트리거 시점 |
| --- | --- | --- | --- |
| **CSV 임포터 비활성** | `Editor/CSVImporterWindow.cs` 전체가 주석. 주석을 풀어도 삭제된 필드(`equipType`, `bagAddedSlots`, `dropEntries`)를 참조해 컴파일되지 않음. 데이터는 인스펙터에서 직접 편집 중 | 지금 SO 구조에 맞게 다시 쓰거나, CSV를 폐기하고 SO를 단일 원본으로 확정 | 몬스터·아이템 수가 늘어 수작업 편집이 부담될 때 |

## 4. 규칙 위반 (주석)

- **스크립트 최상단 요약(Role / Dependencies / Events) 누락**: 30개 파일. 주로 데이터 SO(`Data/Models/*`), 몬스터 패턴 SO, `FogOfWarSystem`, `CombatFeedbackSystem`, `LootableItem`, UI 일부. 해당 파일을 수정할 때 같이 채운다.
