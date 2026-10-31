# P0 구현 설계 초안 (Extraction MVP Core Loop)

> 작성일: 2026-10-31 / 상태: **설계 초안 v2 (Gemini 1차 검토 반영)**
> 근거: [05_Implementation_Gap_Analysis.md](05_Implementation_Gap_Analysis.md) 2.1절 유저 확정 사항
> 목표: `허브(포션 구매) → 던전(전투·파밍) → 탈출/사망 → 결과 확인 → 전리품 판매 → 재진입` 루프를 닫는다.

---

## 0. 작업 단위와 순서

각 단위는 독립 커밋. 앞 단위가 뒤 단위의 전제.

| 순서 | 단위 | 의존 |
| --- | --- | --- |
| W1 | 버그 수정 (B1, B2/B9, B11) | - |
| W2 | 무게 시스템 삭제 + `SlotContainer` 추출 + B10(가방 교체) 수정 + UI 컨테이너 공통화 | W1 |
| W3 | 장비 슬롯 8종 재편 (투구·갑옷·신발·반지·목걸이·귀걸이·가방·무기) | W2 |
| W4 | 데미지 공식 (`CombatMath`) | - |
| W5 | 일시정지 서비스 (`GamePauseService`) + 히트스톱 충돌 해결(B12) | - |
| W6 | 전투 상태 판정 (`PlayerCombatState`) | W4 |
| W7 | 소모품(포션) + 퀵슬롯 1·2·3 | W2, W6 |
| W8 | 우클릭 컨텍스트 메뉴(버리기/판매) + 휴지통 + 반경 드랍 | W2, W5 |
| W9 | 보안 파우치 | W2 |
| W10 | 골드 지갑 + 대장간 판매 + 잡화상 구매 | W2, W8 |
| W11 | 결과 화면 (탈출/사망) | W9, W10 |
| W12 | 문서 동기화 (GDD, task.md, ScriptMaps, 구버전 Arch 문서) | 전체 |

---

## W1. 버그 수정

| # | 수정 방향 |
| --- | --- |
| B1 | `PlayerController.HandleSpecialAttack`: `_stats.CurrentSpeed` → `_stats.CurrentAttackSpeed` |
| B2/B9 | 몬스터→플레이어 데미지가 몬스터 자신의 `EntityCombat` 쿨다운에 막히지 않도록 분리. `EntityCombat`에 쿨다운 무관 즉시 타격 메서드 `int ApplyImmediateHit(GameObject target, int damage, Vector3 dir, bool heavy)` 추가(내부에서 `ApplyDamageToTarget` + `TriggerImpactFeedback`, 실제 적용 데미지 반환). `MonsterController`는 이것만 호출 → 3계층이 상위 계층을 직접 참조하지 않음. 흡혈은 **실제 들어간 데미지** 기준, 0이면 `false` 반환 |
| B11 | `ExplosiveDeathRoutine` 시작 시 `TileSystem.ReleaseTile` 선호출 |
| B8 | W8에서 컨텍스트 메뉴 기반으로 재구현 (슬롯 밖 드래그 = 원위치 복귀 유지) |
| B10 | W2로 이동 (`SlotContainer.Resize` 도입과 함께 처리) |

`TakeDamage`의 반환형을 `void → int`(최종 적용 데미지)로 바꾼다. 기존 호출부(`EntityCombat`, `Projectile`)는 반환값 무시로 호환.

---

## W2. 무게 시스템 삭제 + `SlotContainer` 추출

`InventoryManager`와 `StorageManager`는 슬롯 배열/스택/스왑 로직이 거의 동일하고, 보안 파우치(W9)가 추가되면 3곳 복제가 된다 → **리팩토링 트리거 4** 해당.

```csharp
// Data/Core/SlotContainer.cs  — 순수 C# (POCO)
public class SlotContainer
{
    public int Capacity { get; }
    public event Action OnChanged;
    public bool CanAdd(RuntimeItem item);
    public bool CanPlaceAt(RuntimeItem item, int slot);
    public bool AddAt(RuntimeItem item, int slot);      // 스택 병합 / 내부 스왑 포함
    public bool TryAddAuto(RuntimeItem item);           // 스택 우선 → 앞쪽 빈칸
    public void Remove(RuntimeItem item);
    public bool Resize(int newCapacity, List<RuntimeItem> overflow); // 넘치는 아이템을 overflow로 반환
    public RuntimeItem GetAt(int slot);
    public IReadOnlyList<RuntimeItem> Items { get; }
    public bool Contains(RuntimeItem item);
}
```

- `InventoryManager`, `StorageManager`, `SecurePouchManager`(W9)는 `SlotContainer`를 하나씩 소유하는 얇은 MonoBehaviour로 축소. public API 시그니처(`AddItemAt`, `TryAddNextAvailableSlot`, `RemoveItem`, `GetItemAtSlot`, `MaxSlots`, `OnInventoryChanged`)는 유지해 UI 연결 보호.
- 삭제: `ItemDataSO.weight`, `BackpackDataSO.bagMaxWeight`, `MaxWeight/CurrentWeight`, `PlayerStatManager`의 무게 이속 페널티, `GetEquippedBackpackBonus`의 weight 출력.
- 직렬화된 기존 에셋의 `weight` 값은 필드 삭제 시 무시되므로 에셋 수정 불필요.

**B10 가방 교체·해제 (여기서 처리)**: 용량 변경 전 필요 칸 수를 먼저 검사한다.
- 교체(`TryEquip`, 가방→가방): 필요 칸 = 현재 아이템 수 (새 가방 −1, 기존 가방 +1 → 변동 없음) ≤ 새 용량.
- 해제(`Unequip`): 필요 칸 = 현재 아이템 수 + 1 ≤ 기본 용량.
- 불가 시 `OnEquipmentFailed("가방 공간 부족")` 후 아무것도 바꾸지 않음. 가능 시 `Resize` → 아이템 앞쪽으로 재배치(overflow 0 보장).

**UI 컨테이너 공통화**: `InventoryUIController`와 `StorageUIController`(각 ~360줄)가 거의 동일하고 파우치 UI가 세 번째 복제가 됨 → `ItemContainerUIBase`(슬롯 셀 생성·드래그·드롭 처리)로 추출, 각 UI는 대상 매니저만 지정.
- 드롭 타깃을 성격별 인터페이스로 분리: `IItemContainerUI`(아이템 이동, 기존 유지) / `IItemDropTarget`(아이템을 옮기지 않고 소비·참조: 휴지통, 판매 슬롯, 퀵슬롯). 드래그 종료 시 레이캐스트 결과에서 `IItemDropTarget`을 먼저 검사.
- 패널 `OnDisable`에서 들고 있던 `_heldItemUI`를 원위치 복귀시키고 컨텍스트 메뉴 닫기.

---

## W3. 장비 슬롯 8종

```csharp
// 기존 int 값 보존을 위해 뒤에 추가 (에셋에 int로 직렬화됨)
public enum EquipType { None, Helmet, Armor, Weapon, Ring, Necklace, Backpack, Boots, Earring }
```

- `Armor` = 갑옷(몸통). 슬롯은 무기·투구·갑옷·신발·가방·반지·목걸이·귀걸이 각 1개.
- `ItemCategory`도 동일 원칙으로 끝에 `Valuable` 추가 (W10).
- `ArmorDataSO`(빈 클래스)에 방어구 계열 구분 필드만 추가: `enum ArmorBranch { Explorer, Heavy, Tactical }`. 실제 분기 효과(행동속도 등)는 P1 테크트리 작업에서.
- 획득 경로 데이터: `EquipmentDataSO`에 `bool isCraftOnly` 대신, **드랍 풀에서 `EquipType`이 무기/방어구/가방인 아이템을 거부**하는 검증을 `LootSystem` 로드 시 경고 로그로 처리 (D7).
- 유니티 작업: 인벤토리 패널 프리팹에 `EquipmentSlotUI` 3개(신발·귀걸이·기존 투구 위치 확인) 추가 — unityMCP 필요.

---

## W4. 데미지 공식

```csharp
// Core/CombatMath.cs — static, 상태 없음
public static class CombatMath
{
    public const float MaxNegativeDefAmplify = 2.0f;
    public static int Mitigate(int rawDamage, int defense, int armorPen = 0)
    {
        // rawDamage <= 0 → 0
        // effectiveDef = defense < 0 ? defense : Max(0, defense - armorPen)  (음수 DEF엔 관통 미적용)
        // effectiveDef>=0 : raw * (1 - DEF/(100+DEF))
        // effectiveDef<0  : raw * (2 - 100/(100-DEF)), 최대 2.0배
        // 결과 최소 1
    }
}
```

- 적용 지점: `MonsterController.TakeDamage`(몬스터 `def`), `PlayerStatManager.TakeDamage`(`CurrentDefense`). 경감 → 보호막 흡수 → HP 순서.
- 데미지 텍스트(`OnEntityDamaged`)는 경감 후 최종값 표시.
- ArPen은 파라미터만 열어두고 0 고정 (스탯 추가는 P2).

---

## W5. 일시정지 서비스 + 히트스톱

```csharp
// Core/GamePauseService.cs — static
public static class GamePauseService
{
    public static bool IsPaused { get; }
    public static event Action<bool> OnPauseChanged;
    public static void Request(object owner);   // 요청자 집합(HashSet) 기반, 중복 안전
    public static void Release(object owner);
    public static void ApplyHitStop(float scale);   // 일시정지 중이면 무시
    public static void ClearHitStop();              // 일시정지 중이면 0 유지
}
```

- `Time.timeScale` 쓰기는 이 서비스만 수행. `CameraFollow.HitStopRoutine`은 `ApplyHitStop/ClearHitStop` 호출로 교체 (B12).
- `CameraFollow.OnDisable`의 `Time.timeScale = 1f` 직접 대입도 `ClearHitStop()`으로 교체.
- `DungeonUIManager`의 열고 닫는 경로(Toggle, `CloseInventoryScreen`(Escape), `OpenRewardScreen`)를 단일 `SetInventoryOpen(bool)`로 모아 그 안에서만 Request/Release. `OnDisable`에서도 Release. 씬 언로드 시 `SceneManager.sceneUnloaded`에서 전체 해제(정지 상태로 허브 진입 방지).
- 일시정지 중 영향과 대응:
  - 몬스터 코루틴(`WaitForSeconds`, `Time.deltaTime`): 자동 정지 ✅
  - `EntityCombat.CooldownRoutine`(unscaled): **일시정지 중 누적 중단** (`if (!GamePauseService.IsPaused) elapsed += unscaledDeltaTime`). 히트스톱 중에는 계속 흐름. → Tab으로 몬스터만 멈추고 쿨다운을 소진하는 꼼수 차단
  - 선입력 버퍼: 일시정지 시 버퍼 비우기, `TryConsumeBufferedCommand`는 `IsPaused`면 즉시 반환
  - 우클릭 차징: 인벤토리 열림(`OnPauseChanged(true)`) 시 진행 중인 차징 취소

---

## W6. 전투 상태 판정

```csharp
// Player/PlayerCombatState.cs (플레이어 오브젝트 컴포넌트)
public class PlayerCombatState : MonoBehaviour
{
    [SerializeField] private float _combatHoldDuration = 5f; // 마지막 타격/피격 후 유지 시간
    public bool IsInCombat { get; private set; }
    public event Action<bool> OnCombatStateChanged;
}
```

- `GlobalEvents.OnEntityDamaged`를 구독: 플레이어 피격 또는 몬스터 피격(= 플레이어 타격) 시 타이머 갱신. 몬스터끼리의 데미지(자폭→몬스터)는 현재 없음.
- `Update`에서 `Time.time` 비교만 (GC 0). 일시정지 중엔 `Time.time`이 멈춰 전투 상태가 유지됨 — 의도된 동작.
- 사용처: 포션 규칙(W7), 장비 교체 잠금(`EquipmentManager.TryEquip/Unequip`에서 `IsInCombat`이면 실패 + 메시지).
- 허브에는 플레이어가 없으므로 `EquipmentManager`는 `PlayerCombatState`를 `[RequireComponent]` 없이 optional 참조(`_combatState != null && _combatState.IsInCombat`)로 검사.

---

## W7. 포션 + 퀵슬롯

```csharp
// Data/Models/ConsumableDataSO.cs
public class ConsumableDataSO : ItemDataSO
{
    public int hpRestore; public int mpRestore;
    public float hotDuration = 5f;      // 전투 중 HoT 지속 시간
    public float combatGcd = 7f;        // 전투 중 포션 글로벌 쿨타임
}

// Player/ConsumableController.cs
public class ConsumableController : MonoBehaviour
{
    public bool TryUse(RuntimeItem item);       // 수량 1 차감, 0이면 슬롯에서 제거
    public float GcdRemaining { get; }
    public event Action<float> OnGcdStarted;
    public event Action<bool> OnHotStateChanged;
}
```

규칙 (Items.md 3절):
- 비전투: GCD 없음, 즉시 회복.
- 전투 중: GCD 적용, `hotDuration`에 걸쳐 회복. 회복 중 재사용 시 **남은 회복량은 버리고 타이머·회복량을 새 포션 기준으로 갱신** (중첩 불가).
- 사용 가능 조건: `(hpRestore > 0 && HP < MaxHp) || (mpRestore > 0 && MP < MaxMp)` — 회복할 게 없으면 소모 안 함.
- HoT 진행 중 비전투로 바뀌어도 HoT는 그대로 진행.
- `PlayerStatManager`에 `RestoreMp(int)` 추가.

퀵슬롯:
- `QuickSlotManager`: 3칸, 각 칸은 인벤토리 아이템 **참조**(InstanceID). 아이템은 가방에 그대로 있음.
- 등록: 가방의 소모품을 퀵슬롯 UI로 드래그. 해당 아이템이 가방에서 사라지면 같은 `itemID`의 다른 스택으로 자동 재연결, 없으면 비움.
- 입력: `PlayerInput`에 `OnQuickSlotInput(int index)` 추가 (1·2·3).
- 세션 보존: `PlayerSessionData.QuickSlotItemIds` (itemID 기준).

---

## W8. 컨텍스트 메뉴 / 휴지통 / 반경 드랍

- `ItemContextMenuUI` (인벤토리 Canvas에 1개, 재사용): `DraggableItemUI.OnPointerClick`에서 **우클릭** 시 열림. 버튼은 상황별로 표시:
  - `버리기`: 던전에서만. 허브에서는 숨김.
  - `판매 (N골드)`: 허브 대장간 패널이 열려 있을 때만 (W10).
  - `퀵슬롯 등록 1/2/3`: 소모품일 때 (선택 사항).
- 버리기 흐름 (단방향): UI → `InventoryManager.TryDiscardItem(item)` 커맨드 → `InventoryManager`가 자기 위치·`_discardRadius`로 `GlobalEvents.OnItemDiscardRequested(item, pos, radius, Action<bool>)` 발행 → `LootSystem`이 반경 탐색 → 성공 시 `InventoryManager`가 직접 제거 후 `OnInventoryChanged`, 실패 시 `OnDiscardFailed` 이벤트 → UI 토스트 "공간이 없습니다".
- 반경 탐색: 플레이어 셀 기준 **체비셰프 반경 `_discardRadius`** 내 BFS. 장애물에서 확장 중단, **대각선 확장 시 양옆 직교 타일이 모두 비어 있어야 통과**(벽 모서리 틈새 방지). 반경 내 빈칸 없으면 `false` (기존 `GetNearestEmptyItemTile`의 무조건 `startCell` 반환 경로는 사용 안 함).
  - `_discardRadius`는 **플레이어 오브젝트의 `InventoryManager`**에 둠 ("플레이어 반경으로 조절").
- 휴지통 `TrashSlotUI : IItemDropTarget`: 인벤토리 패널(던전·허브 공통 프리팹). 드롭 시 `InventoryManager.DeleteItem(item)` 커맨드 → 영구 삭제.
- 슬롯 밖 허공 드래그: 원위치 복귀 (현재 동작 유지).

---

## W9. 보안 파우치

- `SecurePouchManager` (`SlotContainer` 보유), `PlayerSessionData.PouchItems`.
- 사망 시: `ClearPlayerLoadoutOnDeath()`는 인벤토리·장비만 비우고 파우치는 유지.
- 허브에서도 파우치를 꺼낼 수 있도록 `HubSystem.EnsureHubManagers`에 `SecurePouchManager` 추가 (퀵슬롯 등록을 허브에서 하므로 `QuickSlotManager`도 추가).
- 초기 칸 수: `[SerializeField] _baseSlots` (값은 유저 확정 필요). 확장 업그레이드는 P1(강화소).
- UI: 인벤토리 패널 우측 하단에 파우치 그리드 (UI_Layout_GDD 2.1). 인벤토리↔파우치 드래그는 `IItemContainerUI` 재사용.
- 제한: 장비(무기·방어구·가방) 장착품은 파우치 이동 불가? → **유저 확정 필요** (아래 질문 참조).

---

## W10. 골드 지갑 + 판매 + 구매

```csharp
// PlayerSessionData에 추가
public static int Gold { get; private set; }
public static event Action<int> OnGoldChanged;
public static void AddGold(int amount);
public static bool TrySpendGold(int amount);
```

- 골드는 지갑에만 존재. 던전에 들고 갈 수 없음(가방에 없으므로 사망 시 손실 없음).
- 기존 "골드 아이템(ID 1)" 드랍: `DropPoolSO.goldItemID/minGold/maxGold` 필드 삭제. 골드 가치는 **고가 전리품 아이템**(`ItemCategory.Material` 아님 → `ItemCategory.Valuable` 신규 추가)의 `baseValue`로 표현.
- **대장간(판매)**: `HubObjectType.Blacksmith` 클릭 → 판매 패널 + 인벤토리·창고 동시 오픈. 우클릭 메뉴 `판매` 또는 판매 슬롯으로 드래그. 판매가 = `baseValue × Count × sellRatio`.
- **잡화상(구매)**: `ShopCatalogSO { List<ShopEntry> entries }`, `[Serializable] struct ShopEntry { ItemDataSO item; int price; }` (튜플은 인스펙터 직렬화 불가). 구매 시 인벤토리 → 실패하면 창고 → 둘 다 꽉 차면 거부.
- HUD: 허브 상단에 보유 골드 표시 (`OnGoldChanged` 구독).

---

## W11. 결과 화면

```csharp
public class RunResult   // POCO
{
    public bool IsExtracted;
    public List<RuntimeItem> BroughtBack;   // 탈출: 가방+파우치+장비
    public List<RuntimeItem> Lost;          // 사망: 가방+장비
    public List<RuntimeItem> Saved;         // 사망: 파우치
    public int TotalValue;                  // Σ baseValue × Count
}
// PlayerSessionData.LastRunResult (허브에서 1회 표시 후 null)
```

- 생성 지점: `DungeonExit.ReturnToHub`, `PlayerStatManager.HandleDeath`. **`ClearEquipment/ClearInventory/ClearPlayerLoadoutOnDeath` 호출 직전**에 `new List<RuntimeItem>(...)`로 스냅샷 (리스트가 인플레이스로 비워지므로).
- 허브 `RunResultUI`가 `Start`에서 `LastRunResult`가 있으면 패널 표시. 사망 시 흑백/붉은 톤, Lost/Saved 대조.

---

## W12. 문서 동기화

- GDD: Combat_System_Idea(바운스 확정, 슬라이딩 삭제, 3타 콤보·차징 추가), Items(무게 없음, 버리기/휴지통, 골드 전리품), Armor(장비 부위, 탐험형=속도 컨셉, 가방 칸 보너스 삭제), Hub(가방 테크트리 종속), GDD.md(경험치 문구 삭제), Economy(골드 전리품 판매 구조).
- `task.md` 체크 갱신, `ScriptMaps` 3종 갱신, 구버전 `Combat_System_Arch`/`Inventory_System_Arch`/`02_Feature_Backlog`/`03_Data_Tables_Spec` 현행화.

---

## 유니티 에디터 작업 (unityMCP 필요)

인벤토리 패널 프리팹에 신발·귀걸이 슬롯, 휴지통, 파우치 그리드, 컨텍스트 메뉴 / 던전 HUD에 퀵슬롯 3칸 + HP·MP 게이지(최소형) / 허브에 판매·상점·결과 패널, 골드 표시 / 포션·고가 전리품 SO 샘플 2~3개, `ShopCatalogSO` 1개.

---

## 유저 확정 필요 (설계 중 발생)

1. 판매가 비율 `sellRatio` 기본값 (예: 1.0 = baseValue 그대로)
2. 퀵슬롯 등록 방식: 드래그 링크(아이템은 가방에 유지) 방식으로 OK?
3. 휴지통 영구 삭제 시 확인 팝업 여부
4. 보안 파우치 초기 칸 수, 그리고 파우치에 넣을 수 없는 아이템 종류(장비류 등) 제한 여부
5. 전투 상태 유지 시간(기본 5초), 포션 GCD, HoT 시간 초기값 — **GCD가 HoT보다 길면 "회복 중 재사용 시 타이머 갱신" 상황이 생기지 않음.** 둘의 대소 관계 의도 확인 필요
6. 상자 여는 입력 — P0 범위 밖이므로 이후 결정
7. D12(몬스터 감지 각도)를 P0에 넣을지 P1로 미룰지
8. 초기 보유 골드, 그리고 제작소가 없는 P0 단계에서 사망으로 기본 무기·방어구·가방을 잃었을 때 재지급 방식 (예: 기본 장비 무료 재지급 / 잡화상 판매)
9. 인벤토리 창에서 포션 직접 사용(더블클릭/우클릭) 허용 여부, 아니면 퀵슬롯 전용
10. 스택 아이템 버리기·판매·삭제 시 스택 전체 처리 vs 수량 분할

## Gemini [IDEA] (구현 안 함, 백로그 추가 여부만 제안)
- `PlayerInput`의 `IsPointerOverGameObject()` 검사가 키보드 이동·퀵슬롯까지 막음 → 마우스 입력만 차단하도록 분리. HUD 위에 커서가 있으면 이동이 안 되는 문제라 W7(퀵슬롯 HUD 추가) 시 체감될 가능성 높음.
