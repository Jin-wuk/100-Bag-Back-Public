# 스크립트 종속성 맵: 플레이어 및 익스트랙션 데이터 시스템 (Player & Extraction System)

본 문서는 플레이어의 이동, 스탯/사망, 인벤토리·장비·창고 관리 및 씬 전환 간 데이터 영속성을 담당하는 스크립트들의 아키텍처 지도입니다.

## 📁 `Assets/01_Scripts/Data/Core/`
- **`SaveData.cs` (`PlayerSessionData`)**: `02_HubScene` ↔ `03_DungeonScene` 씬 전환 간 인벤토리(`InventoryItems`), 장착 장비(`EquippedItems`), 허브 창고(`StorageItems`) 런타임 데이터를 보존하는 영속 컨테이너입니다. 사망 시 `ClearPlayerLoadoutOnDeath()`를 통해 창고 아이템은 보존하고 인벤토리 및 장착 장비를 모두 증발시킵니다.

## 📁 `Assets/01_Scripts/Player/`
- **`PlayerController.cs`**: 플레이어의 상태(Idle, Moving)를 관리하는 진입점(Facade)입니다.
- **`PlayerInput.cs`**: WASD 4방향 이동, 마우스 8방향 조준 및 인벤토리(`I`) 입력을 처리합니다.
- **`PlayerMovement.cs`**: 그리드 타일 기반 이동을 수행합니다.
- **`PlayerStatManager.cs`**: `MaxHp`/`MaxMp`와 `CurrentHp`/`CurrentMp`를 분리 관리하여 아이템 습득 시 체력이 초기화되는 문제를 방지합니다. `CurrentHp <= 0` 도달 시 `HandleDeath()`를 호출하여 가방과 장착 장비를 모두 증발시키고 `02_HubScene`으로 복귀시킵니다.
- **`InventoryManager.cs`**: 플레이어 가방 슬롯 및 무게를 관리하며, `Awake` 시 `PlayerSessionData`에서 아이템과 가방 용량 보너스를 복원하고 변경 시 자동 동기화합니다.
- **`EquipmentManager.cs`**: 장착 장비를 부위별로 관리하며, `Awake` 시 `PlayerSessionData`에서 장착 상태를 복원하고 변경 시 자동 동기화합니다.

## 📁 `Assets/01_Scripts/Hub/` & `Assets/01_Scripts/UI/`
- **`StorageManager.cs`**: 허브 개인 창고 슬롯을 관리하며 `PlayerSessionData.StorageItems`와 동기화합니다.
- **`HubSystem.cs`**: 허브 오브젝트 클릭 이벤트를 수신하며, 허브 씬 내 `StorageManager`/`InventoryManager`/`EquipmentManager` 존재를 보장합니다. 창고(`HubObjectType.Storage`) 클릭 시 창고 UI(`_storageUIPanel`)와 인벤토리/장비 통합 UI(`_inventoryUIPanel`)를 동시에 활성화합니다 (`ESC` 키로 닫기 지원).
- **`InventoryUIController.cs` / `StorageUIController.cs` / `EquipmentUIController.cs`**: `IItemContainerUI` 인터페이스를 통해 인벤토리↔창고 간 드래그 앤 드롭 이동 및 더블클릭 장착/이동을 처리합니다.

---
**[익스트랙션 루프 흐름]**
1. **생환 (탈출 성공)**: `DungeonExit` 도달 ➡️ `PlayerSessionData`에 동기화된 인벤토리·장비 보존 ➡️ `02_HubScene` 로드 ➡️ 창고 클릭 시 창고 UI + 인벤토리/장비 UI 동시 표시 및 아이템 보관.
2. **사망 (탈출 실패)**: `PlayerStatManager.TakeDamage` (`CurrentHp <= 0`) ➡️ `HandleDeath()` ➡️ `EquipmentManager.ClearEquipment()` & `InventoryManager.ClearInventory()` & `PlayerSessionData.ClearPlayerLoadoutOnDeath()` (가방+장착 장비 전량 증발, 창고는 유지) ➡️ `02_HubScene` 복귀.
