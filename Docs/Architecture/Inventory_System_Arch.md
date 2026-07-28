# 인벤토리 및 보상 시스템 설계 명세서 (Inventory System Architecture)

## 1. 아키텍처 개요
**가방 테트리스(Backpack Tetris)**와 **듀얼 팝업 보상(Dual Reward UI)**을 관장하는 시스템입니다. 아이템은 1x1, 1x2 등 고정된 직사각형 폼팩터를 가집니다.

## 2. 주요 클래스 및 역할

### 2.1 ItemData (순수 데이터 / Model)
*   아이템의 형태(너비, 높이), ID, 아이콘, 툴팁 정보 등을 담은 ScriptableObject 또는 순수 데이터 클래스.

### 2.2 InventoryGrid (논리 맵 / Model)
*   실제 UI를 그리지 않는 순수 C# 클래스.
*   2차원 배열(예: `int[6, 6]`)을 쥐고 있으며, 특정 좌표 `(x, y)`에 아이템을 배치할 수 있는지 검사(Collision Check)하는 알고리즘 제공.

### 2.3 InventoryManager (통제탑)
*   **내 가방(Main Inventory)**의 `InventoryGrid` 데이터를 보유하는 싱글톤.
*   아이템 획득, 버리기, 회전 연산을 총괄하며 결과값(성공/실패)을 UI로 전파.

### 2.4 듀얼 UI 패널 (View)
*   전리품 획득 상황(보스 킬, 상자 오픈) 시 **두 개의 패널이 동시 팝업**됩니다.
*   **InventoryUI**: 내 가방의 현재 상태를 렌더링하는 영역.
*   **RewardUI**: 이번에 획득한 전리품들이 띄워지는 임시 영역.
*   **상호작용**: 유저는 마우스 드래그 앤 드롭으로 `RewardUI`에서 `InventoryUI`로 아이템 타일을 옮깁니다.

### 2.5 Dropped Pouch (바닥에 남는 가방)
*   유저가 `RewardUI` 패널을 닫을 때, 패널 안에 아직 줍지 않은 아이템이 남아있다면 `MapManager`에 신호를 보냅니다.
*   해당 좌표에 `Interactable Pouch` 프리팹을 스폰시킵니다.
*   나중에 유저가 이 Pouch를 다시 클릭하면, 저장해두었던 남은 아이템 데이터를 로드하여 `RewardUI`를 다시 열어줍니다.
