# 리팩토링 및 기술 부채 (Refactoring & Tech Debt)

본 문서는 '선(先) 기능 구현, 후(後) 리팩토링' 원칙에 따라, 당장의 속도를 위해 타협했으나 향후 고도화가 필요한 아키텍처 개선 사항을 기록합니다.

## [Hub] 던전 입장 로직 분리
*   **현재 상태**: `DungeonEntranceUI.cs`가 `SceneManager.LoadScene()`을 직접 호출하여 씬을 전환함.
*   **문제점**: 아키텍처 원칙(UI의 역할 제한) 위배. 향후 던전 입장 애니메이션 및 던전 생성 비동기 로직이 추가되면 UI 클래스가 비대해지고 제어 흐름이 꼬임.
*   **개선 예정 사항**:
    1.  `DungeonEntranceUI`는 `GlobalEvents.OnDungeonLoadRequested` 같은 전역 이벤트(커맨드)만 던지도록 수정.
    2.  `DungeonSystem` (또는 전역 `SceneLoadManager`)이 이벤트를 수신.
    3.  해당 Manager/System이 화면 트랜지션 애니메이션 재생 ➡️ 비동기 씬 로드 ➡️ 던전 생성 로직을 순차적으로 통제하도록 분리.
*   **트리거 시점**: [던전 진입 애니메이션 연출] 및 [던전 생성 비동기 로드 로직] 작업 시작 시 수행.

## [Combat] 무기 이펙트(VFX) 동적 오브젝트 풀링 (Object Pooling) 연동
*   **현재 상태**: `EntityCombat.cs` 내부에서 무기 프리팹을 스윙할 때마다 `Instantiate`로 생성하고 일정 시간 후 `Destroy` 함.
*   **문제점**: 공격 속도가 빠른 게임 특성상 매 타격마다 GC 할당(메모리 가비지)과 CPU 오버헤드가 발생하여 성능 저하 및 렉(스터터링) 유발.
*   **개선 예정 사항**:
    1.  전역 또는 전투 시스템(Combat System) 레벨에 `ObjectPoolManager` 도입.
    2.  플레이어가 장착 중인 무기 데이터(`WeaponDataSO`)에서 `weaponPrefab`을 읽어와서, 던전 진입 시점이나 무기 스왑(Equip) 시점에 해당 프리팹들을 동적으로 미리 5~10개 풀링(지연 초기화).
    3.  `EntityCombat.cs`는 `Instantiate` 대신 풀에서 오브젝트를 빌려오고, `Destroy` 대신 비활성화 후 풀로 반환하도록 리팩토링.
*   **트리거 시점**: [다양한 무기 데이터 제작] 이후 혹은 [모바일/저사양 기기 성능 최적화] 단계에서 수행.
