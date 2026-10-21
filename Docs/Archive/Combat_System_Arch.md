# 전투 시스템 설계 명세서 (Combat System Architecture)

## 1. 아키텍처 개요
본 게임의 전투는 맵을 배회하는 액션 RPG가 아니라, **"팝업형 1대1 오토배틀러"**입니다.
이동 및 상호작용(탐험) 상태와 전투(팝업) 상태가 엄격하게 분리됩니다.

## 2. 주요 클래스 및 역할

### 2.1 PlayerController & MonsterController (MonoBehaviour)
*   **PlayerController**: 마우스 클릭 좌표로 이동하는 NavMeshAgent 기반의 이동 컨트롤러.
*   **IInteractable**: 몬스터, 상자, 제단 등에 부착되는 인터페이스. `Interact()` 메서드를 가집니다. 플레이어가 대상과의 상호작용 사거리(Distance) 내에 진입했을 때만 발동합니다.
*   **Monster_Dummy**: `IInteractable`을 구현. 클릭 시 플레이어가 다가오게 만들고, 사거리에 닿으면 `CombatManager.BeginCombat(this)`를 호출합니다.

### 2.2 CombatManager (통제탑)
*   전투 팝업의 상태 기계(FSM)를 관리하는 싱글톤 매니저.
*   **상태 전환**: 
    *   탐험 중 -> 몬스터 조우 -> `CombatManager.ChangeState(CombatState)`
    *   도주 버튼 클릭 -> `CombatManager.ChangeState(IdleState)` (몬스터 체력 100% 리셋)
    *   몬스터 사망 -> 승리 이벤트 발생 후 `IdleState` 복귀.

### 2.3 FSM (상태 기계)
*   `StateMachine`: 현재 상태의 `Update()`를 매 프레임 호출.
*   `IdleState`: 전투 패널이 닫혀있는 평시 상태. 아무 연산도 하지 않음.
*   `CombatState`: 전투 패널이 팝업된 상태. 플레이어와 몬스터의 쿨타임(공격 속도)을 계산하여 자동으로 `TakeDamage()`를 교환.

### 2.4 CombatUI (View)
*   `CombatManager`의 이벤트(OnCombatStarted, OnCombatEnded, OnHealthChanged)를 구독하여 체력 바와 데미지 텍스트를 화면에 렌더링.
*   몬스터에게 마우스를 올리거나(Hover) 꾹 누르면 스탯 정보를 띄워주는 **Tooltip UI** 기능 포함.
*   하단에 `[도주(Flee)]` 버튼을 노출하여, 클릭 시 `CombatManager.Flee()`를 호출.
