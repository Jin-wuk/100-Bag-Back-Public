# 12. UI 키트 적용 및 허브 오브젝트 아트 교체 설계안

> 상태: 구현 완료, 유저 승인 대기 (2026-11-24). 아래 "구현에서 바뀐 점"이 본문보다 우선
> 아트 원본: `Desktop\Art\100BagBack\ui_kit\final\` (28개), `Desktop\Art\100BagBack\_hub\final\` (3개)

## 0. 구현에서 바뀐 점 (2026-11-24)
- **캔버스**: Canvas Scaler는 바꾸지 않았다. 기존 UI가 1920×1080 픽셀 기준으로 만들어져 있어, 캔버스를 1024×576 기준으로 바꾸면 손대지 않는 UI까지 모두 1.875배가 되기 때문(유저 결정). 새 패널(인벤토리·창고·핫바)만 1024×576 기준 단위로 만들고 `localScale` 1080/576을 건다. 1080p에서 보이는 결과는 스펙과 같다.
- **인벤토리 루트**: 씬에서 화면 왼쪽 절반(앵커 0~0.5)으로 늘리고 패널은 가운데 앵커 → 왼쪽 절반 정중앙.
- **창고**: 가방과 같은 키트 단위 패널(292×438, 6열, 8줄 보임 + 스크롤)로 다시 만들어 좌우 대칭.
- **잡화상·대장간**: 내용이 1080 기준(750×950)이라 크기는 두고 배율 0.864로 가방 패널과 같은 화면 높이(821px)에 맞추고, 패널·제목 바·버튼(normal/hover/pressed SpriteSwap)·판매 칸·상세 창(tooltip) 스킨만 교체. 대장간 패널은 가로 648px.
- **기지 오브젝트**: `GeneralStore`·`Storage`·`BlackSmith` 스프라이트를 `hub_shop`·`hub_stash`·`hub_forge`(PPU 32, 하단 중앙 피벗)로 바꾸고, 예전 스프라이트 바닥에 맞춰 내린 뒤 콜라이더를 그림 크기에 맞춤.
- **아이템 아이콘**: `ItemUI.prefab`은 칸 크기를 따르게(앵커 늘림) 바꾸고 개수 글자는 자동 크기(최대 8).
- **적용 방법**: 모두 에디터 메뉴 `Tools/UI Kit/*`로 다시 만들 수 있다. 씬 인스턴스의 하위 RectTransform 오버라이드는 메뉴가 되돌린다(컴포넌트 참조 오버라이드는 유지).
- **보스 프레임**: 6-1절대로 교체 완료.

## 1. 목표
- 인벤토리·장비·보안 파우치·휴지통을 하나의 인벤토리 패널(292×438)로 합치고, 새 UI 키트 스킨을 입힌다.
- 하단 핫바를 1·2·3·DASH 4칸(44×44)으로 통일한다.
- 허브 패널(창고·잡화상·대장간)도 같은 스킨으로 바꾸고, 화면 오른쪽 절반의 중앙에 둔다.
- 허브의 잡화상·창고·대장간 오브젝트를 새 스프라이트로 교체한다.

## 2. 확정 사항 (유저 답변·시안 기준)
| 항목 | 결정 |
| --- | --- |
| Canvas Scaler | ~~두 씬 모두 Scale With Screen Size 1024×576~~ → 변경 없음. 새 패널에만 배율 1080/576 (0절) |
| 인벤토리 패널 | 292×438. 화면 왼쪽 절반의 중앙: x = (screenW/2 − panelW)/2, y = (screenH − panelH)/2 |
| EQUIP | 4열×2행, 38×38, 간격 3. 윗줄 무기·투구·갑옷·신발 / 아랫줄 배낭·반지·목걸이·귀걸이. 빈 칸에는 `ui_equip_hint_*` 오버레이 |
| 스탯 | HP·MP·ATK·DEF를 EQUIP 오른쪽에 세로로 (기존 `PlayerStatsUIController` 재사용) |
| BAG | 6열, 42×42, 간격 3. 헤더 `BAG (사용/현재 최대)`. 최대 가능 칸(기본 + 가장 큰 배낭 보너스)까지 그리고, 현재 칸 수를 넘는 칸은 잠금. 3줄이 보이고 나머지는 스크롤 |
| Secure Pouch | 2칸, 36×36, `ui_slot_thick_equip`. 하단 왼쪽 |
| Trash | 1칸, 36×36, `ui_slot_thin_trash`. 하단 오른쪽 끝 |
| 핫바 | 화면 아래 중앙, 44×44 4칸, `ui_slot_thick_normal`. 키 이름(1·2·3·DASH)은 칸 왼쪽 위 텍스트 |
| 허브 패널 | 창고·잡화상·대장간 패널을 오른쪽 절반 중앙에 두고 패널·헤더·버튼을 9-slice로 교체 |
| 범위 밖 | 툴팁 기능(프레임 에셋만 들여옴), 우클릭 메뉴·수량 창·확인 창 버튼 스킨 |

## 3. 에셋 들여오기
- 위치: `Assets/Art/Sprites/UI/Kit/`(UI 키트), `Assets/Art/Sprites/Hub/`(허브 오브젝트)
- 공통 임포트: Sprite(Single), Filter Point, Compression None, Mip 끔
- UI 키트: PPU 100. 화면에 2배로 보이도록 `Image.pixelsPerUnitMultiplier = 0.5`로 맞춘다(테두리 1px → 화면 2px). Sprite Border는 스펙 마진 그대로:
  - panel 6 / tooltip 4 / btn 4 / key badge 3 / scroll track·thumb 2 / header L4 R2 T2 B2
  - 슬롯: thin 1, thick 2 (테두리 두께만큼 Sliced)
- 허브 오브젝트: PPU 32(타일과 같음), Pivot Bottom Center
- 에디터 메뉴 `Tools/UI Kit/Import Settings`로 위 설정을 일괄 적용한다(재임포트해도 같은 결과가 나오게).

### 슬롯 늘림 문제
32px 슬롯을 36·38·42px로 그리면 정수 배율이 아니다.
- normal·equip·selected는 Sliced로 테두리만 고정하고 안쪽 단색을 늘린다 → 깨짐 없음.
- locked·trash는 가운데에 아이콘이 있어 Sliced로 늘리면 아이콘이 늘어난다. Simple로 그린 뒤 화면에서 확인하고, 깨지면 테두리와 아이콘을 분리한 에셋을 아트 쪽에 요청한다.

## 4. 구조 변경

### 4.1 프리팹
| 프리팹 | 변경 |
| --- | --- |
| `InventoryPanel` | 루트를 `ui_panel_9slice` 292×438로 바꾸고, 섹션 `EquipSection`·`BagSection`·`BottomRow`로 재배치. 기존 `EquipmentPanel` 인스턴스를 EquipSection으로 흡수 |
| `EquipmentPanel` | 슬롯 8칸을 4×2 Grid(38, 간격 3)로, 스탯 텍스트 4개를 오른쪽 세로 배치. 속도 텍스트(`_speedText`)는 시안에 없어 숨김 |
| `Slot`(그리드 셀) | 새 `GridCellUI` 컴포넌트(배경 Image, 잠금 표시)를 붙이고 `ui_slot_thin_normal` 적용 |
| `QuickSlotBar` | 44×44 4칸, 간격 4. 4번째 `DashSlot`에 `DashCooldownUI`를 옮겨 붙임 |
| 허브 씬 패널 | `StorageUIPanel`, `ShopPanel`, `BlacksmithPanel` 스킨 교체·위치 이동 |

### 4.2 코드
| 파일 | 변경 |
| --- | --- |
| `ItemContainerUIBase` | `protected virtual int DisplaySlotCount => _container.MaxSlots;`를 추가. `DrawGrid`는 DisplaySlotCount만큼 셀을 만들되, `MaxSlots` 이상인 셀은 `_cells`에 넣지 않고 `GridCellUI.SetLocked(true)`. → `GetSlotIndex`가 잠긴 칸에 -1을 돌려줘서 드롭이 막힌다(컨테이너 로직 변경 없음). 다시 그리는 조건에 DisplaySlotCount 변화도 포함 |
| `InventoryUIController` | `DisplaySlotCount` = `InventoryManager.MaxPossibleSlots`. BAG 헤더 텍스트 `BAG ({ItemCount}/{MaxSlots})`를 `OnContainerChanged` 때 갱신 |
| `InventoryManager` | `MaxPossibleSlots` = `_baseMaxSlots` + 모든 `BackpackDataSO.bagAddedSlots` 중 최댓값. `Resources.LoadAll`은 Awake에서 한 번만 한다(수치 하드코딩 없음) |
| `GridCellUI` (신규) | 셀 배경·잠금 상태 표시. 잠금이면 `ui_slot_thin_locked` |
| `EquipmentSlotUI` | `_defaultIcon` 대신 `_hintOverlay` Image(실루엣, 알파 반투명). 장착하면 숨김. 필드 추가만 하고 기존 필드는 유지 |
| `QuickSlotSlotUI` | 키 라벨 텍스트 필드 추가(선택) |
| `DashCooldownUI` | 코드 변경 없음, 붙는 위치만 이동 |
| `UIKitImportSettings` (에디터, 신규) | 3절 임포트 설정 일괄 적용 |

스크롤 관련 보완 (Claude 자체 검토):
- 3줄 밖으로 스크롤되어 가려진 셀에 드롭되지 않도록, `GetSlotIndexAtScreenPos`는 셀 검사 전에 스크롤 Viewport 안에 마우스가 있는지 먼저 본다(`_slotViewport` 인스펙터 참조, 비어 있으면 검사 생략 → 창고·파우치는 그대로).
- 다시 그리는 조건은 `MaxSlots` 변화 또는 `DisplaySlotCount` 변화로 한다(`_cells.Count`는 잠기지 않은 칸 수만 셈).

허브의 DASH 칸: 허브에는 대시가 없어 `PlayerInput`이 없으므로 `DashCooldownUI`가 아무것도 하지 않는다. 허브에서는 DASH 칸을 숨긴다(`QuickSlotBarUI`에 `_showDashSlot` 인스펙터 옵션).

### 4.3 이벤트
새 전역 이벤트 없음. 기존 구독(`ISlotItemContainer.OnContainerChanged`, `EquipmentManager.OnEquipmentChanged`, `QuickSlotManager.OnQuickSlotsChanged`)을 그대로 쓴다.

## 5. 영향 범위 (graphify + grep)
- `ItemContainerUIBase` ← `InventoryUIController`, `StorageUIController`, `SecurePouchUIController` 상속. DisplaySlotCount 기본값이 MaxSlots라서 창고·파우치는 동작이 바뀌지 않는다.
- `InventoryPanel.prefab`: 허브·던전 씬 둘 다 사용. `EquipmentPanel.prefab`은 InventoryPanel 안에서만 사용.
- `QuickSlotBar.prefab`: 허브·던전 씬. `DashCooldownUI`: 던전 씬에만 있음.
- `Slot.prefab`(그리드 셀): InventoryPanel, 허브 씬(창고). 창고 셀도 새 스킨이 같이 적용된다.
- ~~Canvas Scaler 변경~~ → 하지 않음(0절). 기존 HUD·상점 등은 크기 변화 없음.
- 허브 오브젝트 `GeneralStore`·`Storage`·`BlackSmith`: SpriteRenderer 스프라이트와 BoxCollider2D 크기만 바꾼다. `InteractableController`·`OutlineEffect`는 그대로.

## 6. 위험·확인 사항
- 대장간(TechTree) 패널은 내용이 많아 오른쪽 절반(512 폭)에 들어가지 않을 수 있다. 들어가지 않으면 크기는 유지하고 스킨만 바꾼 뒤 유저에게 보고한다.
- 1024×576 기준으로 바꾸면 기존 UI 글자 크기가 줄어들 수 있다. 각 화면을 확인한다.
- `OutlineEffect`가 새 스프라이트 모양에 맞게 동작하는지 확인한다.

## 6-1. 보스 아트 교체 (멧돼지·나무, 아웃라인 적용판)
- 원본: `Desktop\Art@BagBack\mon_moss-boarinal`, `mon_amber-treantinal`의 E·N·S 시트(가로 9프레임)
- 시트를 9프레임으로 잘라 기존 프레임 PNG(`Assets/Art/Characters/Monsters/{moss-boar,amber-treant}/`)를 덮어쓴다. W는 기존과 같이 E 좌우 반전.
- `.meta`는 건드리지 않는다 → GUID·PPU·피벗이 유지되어 애니메이션 SO·프리팹 연결이 그대로다. 프레임 크기도 기존과 같다(멧돼지 72, 나무 104).
- 예외: 멧돼지 `E_idle` 시트만 504×56(프레임 56px)이라 크기가 달라 보류.

## 7. 구현 단위와 커밋 계획
1. 에셋 임포트 + 에디터 임포트 설정 메뉴
2. 코드: `GridCellUI`, DisplaySlotCount·잠금 칸, BAG 헤더, `MaxPossibleSlots`, 장비 힌트 오버레이, 핫바 DASH 옵션
3. 씬·프리팹: Canvas Scaler, InventoryPanel 재배치, QuickSlotBar
4. 허브 패널 스킨·위치 + 허브 오브젝트 스프라이트
4-1. 멧돼지·나무 보스 프레임 교체
5. Gemini 검토 반영
6. 문서: `Docs/GDD/Items.md`(BAG 잠금 칸 표시), ScriptMaps(Player·Hub)
