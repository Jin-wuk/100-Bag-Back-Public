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

## [Loot] 바닥 아이템 오브젝트 풀링
*   **현재 상태**: `LootSystem.cs`가 바닥 아이템(`LootableItem`)을 `Instantiate`로 생성하고, 주우면 파괴함. (무기 이펙트·데미지 텍스트·투사체는 `CombatFeedbackSystem`에서 이미 풀링 중)
*   **문제점**: 몬스터 처치·아이템 버리기가 잦아지면 생성·파괴에 따른 GC 할당이 반복됨.
*   **개선 예정 사항**: `CombatFeedbackSystem`의 프리팹별 큐 풀과 같은 방식으로 바닥 아이템 풀을 만들고, 줍기 시 비활성화 후 반환.
*   **트리거 시점**: 드랍량이 많은 몬스터·상자 작업 시 수행.

## [Data] CSV 임포터 비활성 상태
*   **현재 상태**: `Editor/CSVImporterWindow.cs`의 코드 전체가 주석 처리되어 있음. 데이터 에셋(`Resources/Data_SO/`)은 인스펙터에서 직접 편집 중.
*   **문제점**: `Resources/CSV/`의 기획 CSV와 실제 SO 에셋 값이 어긋나도 알 수 없음.
*   **개선 예정 사항**: 현재 SO 필드 구조(`MonsterDataSO`의 이동 성향·패턴 목록 등)에 맞게 임포터를 다시 작성하거나, CSV를 폐기하고 SO를 단일 원본으로 확정.
*   **트리거 시점**: 몬스터·아이템 수가 늘어 수작업 편집이 부담될 때 수행.
