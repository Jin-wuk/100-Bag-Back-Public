# 스크립트 종속성 맵: 플레이어 · 아이템 · 익스트랙션 (Player & Items)

플레이어 이동·전투·스탯, 가방·장비·창고·파우치·퀵슬롯, 기지 거래, 씬 간 데이터 영속성을 담당하는 스크립트 지도입니다.
전체 구조와 `GlobalEvents` 목록은 [ARCHITECTURE.md](../../ARCHITECTURE.md)를 참고하세요.

## 1. 데이터와 영속성 — `Data/Core/`

| 스크립트 | 역할 |
| --- | --- |
| `SaveData.cs` (`PlayerSessionData`) | 씬 전환 간 영속 정적 컨테이너. 가방(`InventoryItems`), 장착 장비(`EquippedItems`), 창고(`StorageItems`), 보안 파우치(`PouchItems`), 퀵슬롯 등록, 지갑 골드(`Gold`, 시작 1000, `OnGoldChanged`), 현재 지역·층, 직전 결과(`LastRunResult`), 제작소 해금 노드 ID(`IsNodeUnlocked`/`MarkNodeUnlocked`, 사망과 무관)와 지역 진행도(`UnlockedRegionCount` = 1, `IsRegionUnlocked`). 사망 시 `ClearPlayerLoadoutOnDeath()`로 가방·장착 장비만 비움 |
| `RunResult.cs` | 한 판의 결과 기록(POCO). 탈출이면 가져온 전리품과 총 가치, 사망이면 잃은 것과 파우치로 지킨 것. 아이템 사본으로 저장 |
| `RuntimeItem.cs` | 획득한 아이템 1개: 원본 SO + 고유 ID + 슬롯 위치 + 개수 |
| `SlotContainer.cs` / `ISlotItemContainer.cs` | 칸 배열, 겹치기(`StackLimitOf`: 기본 `maxStack`, 최소 겹침 한도를 받은 창고는 겹치는 아이템·재료를 그 수까지), 이동·교환, 크기 변경(`Resize`, 줄어들면 넘친 아이템 반환). 한도를 넘는 묶음은 한 칸에 넣지 않고 `TryAddAuto`가 나눠 배치. `GetAcceptableAmount`로 받을 수 있는 수량 조회. itemID 기준 합계·차감(`CountByItemId`·`RemoveByItemId`, 슬롯 순서로 여러 묶음에서 덜고 모자라면 무변경). 인터페이스는 `CountItemId`·`RemoveItemIdAmount`와 일괄 변경(`BeginBulkChange`/`EndBulkChange`: 중첩 카운터, 그동안 알림·세션 저장 보류 후 바뀐 게 있으면 1회 발행). 가방·창고·파우치가 공유 |
| `SlotItemTransfer.cs` | 컨테이너 사이 이동(static). 전량이 들어가면 그대로 옮기고, 아니면 들어갈 수량만 새 묶음으로 떼어 옮긴 뒤 원본에서 덜어냄 |
| `GameEnums.cs` | `ItemCategory`(… `Blueprint` = 무기 설계도), `EquipType`(8부위), `ItemRarity`, `ArmorBranch`, `GameState` 등 |

## 2. 플레이어 — `Player/`, `Combat/`, `Core/`

| 스크립트 | 역할 |
| --- | --- |
| `PlayerInput.cs` | WASD 4방향 이동, `Space` 대시(누른 방향, 없으면 마지막으로 누른 방향), 마우스 8방향 조준, 좌클릭 공격, 우클릭 차징 홀드/릴리즈, `Z` 투사체, `1` `2` `3` 퀵슬롯, `Tab` 가방. 입력을 이벤트로만 전달 |
| `PlayerController.cs` | 상태(`Idle`/`Moving`/`Attacking`/`Charging`/`Dashing`)와 단일 슬롯 선입력 버퍼(`BufferedCommand`, 0.22초). 3타 콤보(1.0/1.15/1.5배, 1.5초 유지), 차징(0.6초, 전방 2칸, 2.0배), 무기 히트박스(`WeaponHitboxPattern`) 계산. 대시(`HandleDashInput`: 착지 칸 계산 `TryFindDashLanding`, 쿨타임 `Update`, 공격 주기는 두고 이동 잠금만 푸는 `_moveLockReleased`). 공격 주기가 남은 공격·차징 선입력은 주기 종료 때 소비 |
| `PlayerMovement.cs` | 칸 단위 이동과 보행 연출(칸마다 뛰는 포물선. 프리팹 값은 높이 0.08칸, 틸트·스쿼시 0). 걷기 예약 칸(`ReservedCell`) 기록. 대시 이동(`Dash`: 착지 칸 점유 후 이전 칸 해제, 직선 보간, 사망 시 중단, 끝나면 `OnTileReached` → `OnDashFinished`). 대시 중 `CurrentTargetTileWorldPos`는 착지 칸(위치 예측 패턴용). 걸음·대시의 시작→도착 방향 `CurrentStepDirection`, 지금 칸의 진행도 `CurrentStepProgress`와 걸음 번호 `StepCount`(스프라이트 애니메이션용) |
| `PlayerSpriteVisual.cs` | 스프라이트 프레임만 교체(색·스케일 연출은 건드리지 않음). `SpriteAnimSetSO`(`Data_SO/AnimSO/Player_AnimSet`)의 4방향 대기·이동·공격 프레임 사용. 방향은 마우스 조준이 아니라 마지막 행동(걸음·대시·공격) 방향. 대기 = 대기 프레임 반복(비어 있으면 이동 0프레임), 이동 = 칸 하나를 건너는 동안 걸음 방향 이동 프레임을 한 번 재생(도약→착지, 칸 진행도 기준이라 이동 속도와 자동으로 맞음). 이동 프레임은 9프레임 한 사이클(`chr_explorer_move_<방향>_01~09`). `_moveCycleMode` = `HalfCyclePerTile`이면 칸마다 반 사이클: 같은 방향으로 이어 걸으면(쉰 시간 ≤ `StepChainGap` 0.1초) 앞 절반(0~4)·뒤 절반(4~8)을 번갈아 쓰고, 방향이 바뀌거나 멈췄다 걸으면 앞 절반부터. `FullCyclePerTile`이면 칸마다 전체(이어 걷기에 `_moveAlt`가 있으면 번갈아 씀, 지금은 비어 있음), 대시 = 이동 1프레임 고정, 공격 = `EntityCombat.OnMeleeAttack`·`OnRangedAttack`마다 처음부터 1회 재생 후 공격 잠금 동안 마지막 프레임 유지. 대각선은 좌우 우선. 콤보 단계와 무관하게 같은 모션 |
| `EntityCombat.cs` | 공격 판정과 후딜레이(`Time.unscaledDeltaTime`). 공격 대상이 `IAttackInteractable`(상자)이면 피해 대신 상호작용 호출. 몬스터용 즉시 타격(`ApplyImmediateHit`) |
| `PlayerStatManager.cs` | 기본 스탯 + 장비 합산, 피격 시(`IsInvincible`은 `DungeonGenerator`가 켜고 끔, `IsHitInvincible`은 피격마다 0.5초, 둘 중 하나면 무시) `CombatMath.Mitigate`로 방어력 경감 후 실제 적용 피해 반환, `OnEntityDamaged` 발행. HP 0이면 결과 기록 → 가방·장비 초기화 → 기지 복귀 |
| `PlayerCombatState.cs` | `OnEntityDamaged`를 구독해 마지막 타격·피격 후 5초간 전투 중 판정, `OnCombatStateChanged` |
| `CombatMath.cs` | `DEF/(100+DEF)` 점감, 음수 방어력 증폭(최대 2.0배), 관통력 |
| `GamePauseService.cs` | `Time.timeScale` 단일 소유. 일시정지 요청자 집합 + 히트스톱 |
| `CombatFeedbackSystem.cs` / `CameraFollow.cs` | 데미지 텍스트·이펙트·투사체 풀, 카메라 셰이크와 히트스톱 요청(`OnCombatImpact` 구독) |

## 3. 아이템 — `Player/`

| 스크립트 | 역할 |
| --- | --- |
| `InventoryManager.cs` | 가방. `SlotContainer` 사용, 배낭 보너스로 칸 수 변경, 나누기(`GetSplitMaxAmount`·`TrySplitFrom`: 가방·창고·파우치에서 떼어 가방 빈칸으로, 가방 안에서는 원본 1개 이상 유지), 줍기(`OnItemPickedUpByPlayer` 구독), 버리기(`OnItemDiscardRequested` 발행, 반경 2칸, 기지에서는 `CanDiscard = false`), 휴지통 삭제 |
| `EquipmentManager.cs` | 8부위 장착. 던전 프리팹에서는 장착·해제 잠금(기지 전용). 배낭 교체 시 칸 축소로 넘친 아이템은 `OnItemDroppedOnGround` |
| `SecurePouchManager.cs` | 보안 파우치 2칸, 장비 거부, `PouchItems` 동기화 |
| `QuickSlotManager.cs` | 퀵슬롯 1·2·3을 itemID로 연결. 사용 시 가방의 같은 종류 앞 칸부터 소모. 파우치는 세지 않음 |
| `ConsumableController.cs` | 포션 규칙: 전투 중 지속 회복(`hotDuration` 5초) + 공용 쿨타임(`combatGcd` 7초), 비전투 즉시 회복. 회복할 것이 없으면 소모 안 함. 귀환석(`ReturnStoneDataSO`): 전투 중·보스층(`RegionConfigSO.IsBossFloor`)이면 `OnUseFailed`, 아니면 `OnReturnStoneRequested` → `ExitConfirmUI` 확인 시 `ConfirmReturnStone`(1개 소모 → `RunResult` 기록 → 허브 로드) |

## 4. 기지 — `Hub/`

| 스크립트 | 역할 |
| --- | --- |
| `HubSystem.cs` | `OnHubObjectClicked` 분기(던전 포탈, 창고, 대장간 = 제작소, 잡화상). 플레이어가 없는 기지에서도 가방·장비·창고·파우치·퀵슬롯 매니저 존재 보장 |
| `HubTradeSystem.cs` | 잡화상 판매: `baseValue` × 수량 → 지갑 골드. 구매: `ShopCatalogSO` 가격, 가방 → 창고 순으로 넣고 둘 다 차면 취소. `OnTradeFailed`, `OnTradeCompleted` |
| `CraftingSystem.cs` 외 제작소 | 테크트리 해금·재제작. 상세는 [Hub_System_Map.md](Hub_System_Map.md) |
| `StorageManager.cs` | 창고(30칸). `StorageItems`와 동기화. `_stackLimit`(10)을 `SlotContainer` 최소 겹침 한도로 넘겨 재료를 등급과 무관하게 겹침 |

## 5. UI — `UI/`

| 스크립트 | 역할 |
| --- | --- |
| `ItemContainerUIBase.cs` / `IItemContainerUI.cs` / `IItemDropTarget.cs` | 컨테이너 UI 공통: 드래그 앤 드롭, 더블클릭, `E` 빠른 이동, 드롭 대상(휴지통·판매 칸·퀵슬롯). 다른 컨테이너로의 이동은 모두 `SlotItemTransfer`(일부만 들어가도 이동, 드래그는 이동 전 아이콘 원위치). `IItemContainerUI.SlotContainer`로 원본 컨테이너를 넘김 |
| `InventoryUIController.cs` | 가방 뷰. `TryOpenItemMenu`로 우클릭 메뉴(`ItemContextMenuUI`)를 연다 — 가방 아이템은 버리기·판매·나누기, 창고·파우치 아이템은 나누기만(두 UI가 이 메서드를 호출). 나누기는 `InventoryManager.TrySplitFrom`(가방 앞쪽 빈칸에만, 합치지 않음), 최대치 1이면 수량 창 생략. `CloseItemActions`로 창고 패널이 닫힐 때 메뉴·수량 창 정리 |
| `StorageUIController.cs` / `SecurePouchUIController.cs` / `EquipmentUIController.cs` / `EquipmentSlotUI.cs` / `DraggableItemUI.cs` | 창고, 파우치, 장비창, 장비 칸, 끌리는 아이템 |
| `ItemQuantityDialogUI.cs` / `TrashSlotUI.cs` / `SellSlotUI.cs` | 수량 선택 확인창, 휴지통(확인 후 영구 삭제), 판매 칸 |
| `QuickSlotBarUI.cs` / `QuickSlotSlotUI.cs` | 퀵슬롯 HUD: 아이콘, 가방 내 총 수량, 공용 쿨타임 |
| `DashCooldownUI.cs` | 대시 쿨타임 HUD(`DashSlot`, 퀵슬롯 바 오른쪽). `PlayerInput.OnInputCreated`로 플레이어를 찾아 `DashCooldownNormalized`를 Filled 이미지에 채움 |
| `ShopPanelUI.cs` / `GoldDisplayUI.cs` / `RunResultUI.cs` / `UIToast.cs` | 잡화상 목록(스크롤 영역, 하단에 판매 칸), 지갑 골드, 결과 화면(한 번 표시 후 비움), 알림 |
| `DungeonUIManager.cs` | 던전 가방 패널. 열려 있는 동안 이동 차단 + `GamePauseService` 일시정지 요청. 플레이어의 `PlayerCombatState`를 캐싱해 전투 중이면 Tab·보상 화면 열기를 거부하고 `UIToast`(던전 HUD의 FloorToast)로 안내. 소모품 사용 실패 사유(`ConsumableController.OnUseFailed`)도 같은 토스트로 표시. 닫기는 항상 허용(단 확인창·층 전환의 입력 잠금 중에는 Esc 무시) |
| `PlayerStatsUIController.cs` | HP·MP·스탯 텍스트 |

---

**익스트랙션 루프 흐름**
1. **생환**: `DungeonExit` 도달(또는 귀환석 `ConsumableController.ConfirmReturnStone`) → `RunResult`(탈출, 지역·층 포함) 기록 → `PlayerSessionData`에 동기화된 가방·장비·파우치 보존 → `02_HubScene` 로드 → `RunResultUI` 표시 → 잡화상에서 전리품 판매·포션 구매(`HubTradeSystem`) → 제작소에서 해금·재제작(`CraftingSystem`).
2. **사망**: `PlayerStatManager.TakeDamage`(`CurrentHp <= 0`) → `HandleDeath()` → `RunResult`(사망: 잃은 것 / 파우치로 지킨 것) 기록 → `EquipmentManager.ClearEquipment()` · `InventoryManager.ClearInventory()` · `PlayerSessionData.ClearPlayerLoadoutOnDeath()` (가방·장착 장비 삭제, 파우치·창고·지갑 골드 유지) → `02_HubScene` 복귀 → `RunResultUI` 표시.
