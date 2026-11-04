# 스크립트 종속성 맵: 플레이어 · 아이템 · 익스트랙션 (Player & Items)

플레이어 이동·전투·스탯, 가방·장비·창고·파우치·퀵슬롯, 기지 거래, 씬 간 데이터 영속성을 담당하는 스크립트 지도입니다.
전체 구조와 `GlobalEvents` 목록은 [ARCHITECTURE.md](../../ARCHITECTURE.md)를 참고하세요.

## 1. 데이터와 영속성 — `Data/Core/`

| 스크립트 | 역할 |
| --- | --- |
| `SaveData.cs` (`PlayerSessionData`) | 씬 전환 간 영속 정적 컨테이너. 가방(`InventoryItems`), 장착 장비(`EquippedItems`), 창고(`StorageItems`), 보안 파우치(`PouchItems`), 퀵슬롯 등록, 지갑 골드(`Gold`, 시작 1000, `OnGoldChanged`), 현재 지역·층, 직전 결과(`LastRunResult`). 사망 시 `ClearPlayerLoadoutOnDeath()`로 가방·장착 장비만 비움 |
| `RunResult.cs` | 한 판의 결과 기록(POCO). 탈출이면 가져온 전리품과 총 가치, 사망이면 잃은 것과 파우치로 지킨 것. 아이템 사본으로 저장 |
| `RuntimeItem.cs` | 획득한 아이템 1개: 원본 SO + 고유 ID + 슬롯 위치 + 개수 |
| `SlotContainer.cs` / `ISlotItemContainer.cs` | 칸 배열, 겹치기(`StackLimitOf`: 기본 `maxStack`, 최소 겹침 한도를 받은 창고는 겹치는 아이템·재료를 그 수까지), 이동·교환, 크기 변경(`Resize`, 줄어들면 넘친 아이템 반환). 한도를 넘는 묶음은 한 칸에 넣지 않고 `TryAddAuto`가 나눠 배치. `GetAcceptableAmount`로 받을 수 있는 수량 조회. 가방·창고·파우치가 공유 |
| `SlotItemTransfer.cs` | 컨테이너 사이 이동(static). 전량이 들어가면 그대로 옮기고, 아니면 들어갈 수량만 새 묶음으로 떼어 옮긴 뒤 원본에서 덜어냄 |
| `GameEnums.cs` | `ItemCategory`, `EquipType`(8부위), `ItemRarity`, `ArmorBranch`, `GameState` 등 |

## 2. 플레이어 — `Player/`, `Combat/`, `Core/`

| 스크립트 | 역할 |
| --- | --- |
| `PlayerInput.cs` | WASD 4방향 이동, 마우스 8방향 조준, 좌클릭 공격, 우클릭 차징 홀드/릴리즈, `Z` 투사체, `1` `2` `3` 퀵슬롯, `Tab` 가방. 입력을 이벤트로만 전달 |
| `PlayerController.cs` | 상태(`Idle`/`Moving`/`Attacking`/`Charging`)와 단일 슬롯 선입력 버퍼(`BufferedCommand`, 0.22초). 3타 콤보(1.0/1.15/1.5배, 1.5초 유지), 차징(0.6초, 전방 2칸, 2.0배), 무기 히트박스(`WeaponHitboxPattern`) 계산 |
| `PlayerMovement.cs` | 칸 단위 이동과 보행 연출(바운스, 틸트, 스쿼시) |
| `EntityCombat.cs` | 공격 판정과 후딜레이(`Time.unscaledDeltaTime`). 공격 대상이 `IAttackInteractable`(상자)이면 피해 대신 상호작용 호출. 몬스터용 즉시 타격(`ApplyImmediateHit`) |
| `PlayerStatManager.cs` | 기본 스탯 + 장비 합산, 피격 시 `CombatMath.Mitigate`로 방어력 경감 후 실제 적용 피해 반환, `OnEntityDamaged` 발행. HP 0이면 결과 기록 → 가방·장비 초기화 → 기지 복귀 |
| `PlayerCombatState.cs` | `OnEntityDamaged`를 구독해 마지막 타격·피격 후 5초간 전투 중 판정, `OnCombatStateChanged` |
| `CombatMath.cs` | `DEF/(100+DEF)` 점감, 음수 방어력 증폭(최대 2.0배), 관통력 |
| `GamePauseService.cs` | `Time.timeScale` 단일 소유. 일시정지 요청자 집합 + 히트스톱 |
| `CombatFeedbackSystem.cs` / `CameraFollow.cs` | 데미지 텍스트·이펙트·투사체 풀, 카메라 셰이크와 히트스톱 요청(`OnCombatImpact` 구독) |

## 3. 아이템 — `Player/`

| 스크립트 | 역할 |
| --- | --- |
| `InventoryManager.cs` | 가방. `SlotContainer` 사용, 배낭 보너스로 칸 수 변경, 줍기(`OnItemPickedUpByPlayer` 구독), 버리기(`OnItemDiscardRequested` 발행, 반경 2칸, 기지에서는 `CanDiscard = false`), 휴지통 삭제 |
| `EquipmentManager.cs` | 8부위 장착. 던전 프리팹에서는 장착·해제 잠금(기지 전용). 배낭 교체 시 칸 축소로 넘친 아이템은 `OnItemDroppedOnGround` |
| `SecurePouchManager.cs` | 보안 파우치 2칸, 장비 거부, `PouchItems` 동기화 |
| `QuickSlotManager.cs` | 퀵슬롯 1·2·3을 itemID로 연결. 사용 시 가방의 같은 종류 앞 칸부터 소모. 파우치는 세지 않음 |
| `ConsumableController.cs` | 포션 규칙: 전투 중 지속 회복(`hotDuration` 5초) + 공용 쿨타임(`combatGcd` 7초), 비전투 즉시 회복. 회복할 것이 없으면 소모 안 함 |

## 4. 기지 — `Hub/`

| 스크립트 | 역할 |
| --- | --- |
| `HubSystem.cs` | `OnHubObjectClicked` 분기(던전 포탈, 창고, 대장간, 잡화상). 플레이어가 없는 기지에서도 가방·장비·창고·파우치·퀵슬롯 매니저 존재 보장 |
| `HubTradeSystem.cs` | 판매: `baseValue` × 수량 → 지갑 골드. 구매: `ShopCatalogSO` 가격, 가방 → 창고 순으로 넣고 둘 다 차면 취소. `OnTradeFailed`, `OnTradeCompleted` |
| `StorageManager.cs` | 창고(30칸). `StorageItems`와 동기화. `_stackLimit`(10)을 `SlotContainer` 최소 겹침 한도로 넘겨 재료를 등급과 무관하게 겹침 |

## 5. UI — `UI/`

| 스크립트 | 역할 |
| --- | --- |
| `ItemContainerUIBase.cs` / `IItemContainerUI.cs` / `IItemDropTarget.cs` | 컨테이너 UI 공통: 드래그 앤 드롭, 더블클릭, `E` 빠른 이동, 드롭 대상(휴지통·판매 칸·퀵슬롯). 다른 컨테이너로의 이동은 모두 `SlotItemTransfer`(일부만 들어가도 이동, 드래그는 이동 전 아이콘 원위치). `IItemContainerUI.SlotContainer`로 원본 컨테이너를 넘김 |
| `InventoryUIController.cs` | 가방 뷰. 우클릭 시 `ItemContextMenuUI`(버리기·판매) |
| `StorageUIController.cs` / `SecurePouchUIController.cs` / `EquipmentUIController.cs` / `EquipmentSlotUI.cs` / `DraggableItemUI.cs` | 창고, 파우치, 장비창, 장비 칸, 끌리는 아이템 |
| `ItemQuantityDialogUI.cs` / `TrashSlotUI.cs` / `SellSlotUI.cs` | 수량 선택 확인창, 휴지통(확인 후 영구 삭제), 판매 칸 |
| `QuickSlotBarUI.cs` / `QuickSlotSlotUI.cs` | 퀵슬롯 HUD: 아이콘, 가방 내 총 수량, 공용 쿨타임 |
| `ShopPanelUI.cs` / `GoldDisplayUI.cs` / `RunResultUI.cs` / `UIToast.cs` | 잡화상 목록, 지갑 골드, 결과 화면(한 번 표시 후 비움), 알림 |
| `DungeonUIManager.cs` | 던전 가방 패널. 열려 있는 동안 이동 차단 + `GamePauseService` 일시정지 요청. 플레이어의 `PlayerCombatState`를 캐싱해 전투 중이면 Tab·보상 화면 열기를 거부하고 `UIToast`(던전 HUD의 FloorToast)로 안내. 닫기는 항상 허용 |
| `PlayerStatsUIController.cs` | HP·MP·스탯 텍스트 |

---

**익스트랙션 루프 흐름**
1. **생환**: `DungeonExit` 도달 → `RunResult`(탈출) 기록 → `PlayerSessionData`에 동기화된 가방·장비·파우치 보존 → `02_HubScene` 로드 → `RunResultUI` 표시 → 대장간에서 전리품 판매(`HubTradeSystem`) → 잡화상에서 포션 구매.
2. **사망**: `PlayerStatManager.TakeDamage`(`CurrentHp <= 0`) → `HandleDeath()` → `RunResult`(사망: 잃은 것 / 파우치로 지킨 것) 기록 → `EquipmentManager.ClearEquipment()` · `InventoryManager.ClearInventory()` · `PlayerSessionData.ClearPlayerLoadoutOnDeath()` (가방·장착 장비 삭제, 파우치·창고·지갑 골드 유지) → `02_HubScene` 복귀 → `RunResultUI` 표시.
