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
