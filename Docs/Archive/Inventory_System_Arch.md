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

### 2.4 통합 인벤토리 패널 (View)
*   전리품 획득 상황(보스 킬, 상자 오픈) 시 **두 개의 패널이 동시 팝업**됩니다 (메인 인벤토리와 보상 인벤토리).
*   **DualUIPanel (INVENTORY Tab)**: 화면 한쪽에는 현재 착용한 장비(GearSlotUI)가 수직으로 렌더링되고, 다른 쪽에는 내 가방(MainInventoryUI)의 상태가 렌더링되는 통합 뷰입니다.
*   **상호작용**: 유저는 마우스 드래그 앤 드롭으로 아이템을 옮기며 짐을 정리합니다. 또한 아이템을 더블클릭(`IPointerClickHandler`)하여 즉시 장착/해제(Equip/Unequip)를 수행할 수 있습니다.

### 2.5 Dropped Pouch (바닥에 남는 가방)
*   유저가 보상 패널(RewardUI)을 닫을 때, 아직 줍지 않은 아이템이 남아있거나 가방 슬롯 교체로 인해 아이템이 밀려났을(Overflow) 경우 `MapManager` 측에 신호를 보냅니다.
*   해당 좌표에 `Interactable Pouch` 프리팹을 스폰시킵니다.
*   나중에 유저가 이 Pouch를 다시 클릭하면, 저장해두었던 남은 아이템 데이터를 로드하여 다시 파밍 화면을 열어줍니다.

### 2.6 GearManager (장비 통제탑)
*   캐릭터가 착용한 장비(무기, 투구, 갑옷, 악세서리, 가방)를 논리적으로 관리하는 매니저입니다.
*   장비 장착 시 플레이어 스탯(`PlayerStats`)에 즉각적으로 능력치를 가감합니다.
*   가방(Backpack) 장착 시, `InventoryManager.EquipBackpack`을 호출하여 즉시 메인 인벤토리의 그리드 크기를 동적으로 확장/축소(Resize)시킵니다.
