# 구현 현황 및 기획 갭 분석 (Implementation Gap Analysis)

> 작성일: 2026-10-31 / 기준 브랜치: `chore/claude-gemini-pipeline` (530753a)
> 대상: `Assets/01_Scripts/` 전체(약 10,000줄) ↔ `Docs/GDD/*`, `Docs/task.md`, `Docs/Architecture/*`
> 범례: ✅ 구현 / 🟡 부분 구현 또는 기획과 다름 / ❌ 미구현 / ⚪ Post-MVP(기획상 후순위)

---

## 1. 구현 완료 영역 (✅)

| 영역 | 내용 | 근거 스크립트 |
| --- | --- | --- |
| 타일 이동 | WASD/방향키 4방향 1칸 이동, 이동 중 입력 쿨다운 | `PlayerInput`, `PlayerMovement` |
| 에이밍·카이팅 | 마우스 8방향 조준, 이동/조준 분리, 사거리 타일 프리뷰 | `PlayerInput`, `MovementPreview` |
| 선입력 | 단일 슬롯 `BufferedCommand`(0.22s), 타일 안착 시 즉시 발동 | `PlayerController` |
| 평타·강공격 | 좌클릭 3타 콤보(1.0/1.15/1.5x), 우클릭 홀드 차징(2칸 직선 2.0x) | `PlayerController` |
| 무기 히트박스 | `SingleTile` / `ForwardLine2` / `WideSweep3`, 무기별 쉐이크·히트스톱 배율 | `WeaponDataSO`, `EntityCombat` |
| 타격감·풀링 | 이펙트/데미지 텍스트/투사체 풀링, 카메라 쉐이크, 히트스톱 | `CombatFeedbackSystem`, `Projectile`, `CameraFollow` |
| 몬스터 AI | SO 조립식 패턴 6종, 이동 스타일 3종, A*, 가중치 추첨, 인터럽트 코루틴 정리 | `MonsterController`, `MonsterLocomotion`, `Patterns/*` |
| 장판 예고 | 타일 장판, 레이저 조준선, 착탄 장판 | `AttackWarningUI`, 패턴 SO |
| 전장의 안개 | 3단계 안개, Bresenham LOS, 북쪽 2타일 보정, 셰이더, 보조 광원, 엔티티 은폐 | `FogOfWarSystem`, `FogVisibilityObject`, `FogVisionSource` |
| 고정형 맵 생성 | Zone 기반 스폰, BFS 도달 판정, 최원거리 탈출구, 몬스터 간격·안전 반경 | `DungeonGenerator`, `DungeonZone` |
| 인벤토리 | 1슬롯 1아이템, 스택, 드래그&드롭, 더블클릭 장착, E키 빠른 이동 | `InventoryManager`, `InventoryUIController` |
| 장비 | 부위별 장착/해제, 스탯 합산 | `EquipmentManager`, `PlayerStatManager` |
| 루팅 | DropPool(확정 골드/확정 템/가중치 그룹 롤), 필드 드랍, 타일 밟아 자동 획득 | `LootSystem`, `LootableItem`, `TileSystem` |
| 익스트랙션 루프 | 허브↔던전 씬 전환, `PlayerSessionData` 영속, 탈출 시 보존, 사망 시 가방+장비 증발(창고 유지) | `SaveData`, `DungeonExit`, `PlayerStatManager` |
| 허브 창고 | 창고 UI + 인벤토리 동시 오픈, 상호 이동 | `HubSystem`, `StorageManager`, `StorageUIController` |

---

## 2. 기획과 다르게 구현된 부분 (🟡 — 유저 확정 필요)

| # | 항목 | 기획서 | 현재 코드 |
| --- | --- | --- | --- |
| D1 | 가방 용량 결정 방식 | `기본 슬롯 + 방어구(탐험형) 보너스` (Hub.md, Armor.md). 가방 장비 개념 없음 | `EquipType.Backpack` 장비(`BackpackDataSO.bagAddedSlots`)로 확장. 기본 30칸 |
| D2 | 무게(Weight) 시스템 | 언급 없음. "1칸 룰"만 존재 | 아이템 `weight`, 가방 `MaxWeight`, 무게 초과 시 획득 불가 + 이동속도 페널티 |
| D3 | 골드 | 소비 재화(지갑). 매각·감정·재제작에 사용 | 골드 = 아이템(ID 1). 가방 슬롯 차지, 사망 시 증발 |
| D4 | 장비 부위 | 무기 / 방어구 / 반지·목걸이·귀걸이 | `Helmet, Armor, Weapon, Ring, Necklace, Backpack` (귀걸이 없음, 투구·가방 추가) |
| D5 | 이동 연출 | 고속 슬라이딩(Dash) + 잔상, "통통 튀는 모션 폐기" (Combat_System_Idea.md) | Y축 바운스(`_hopHeight`) + 워블 + 스쿼시&스트레치 |
| D6 | 인벤토리 오픈 시 | 필드 시간 정지(Pause) | 플레이어 이동만 차단, 몬스터는 계속 행동 |
| D7 | 무기 획득 경로 | 무기·방어구는 드랍되지 않고 테크트리 해금·제작 (Weapons.md, Armor.md) | 드랍 풀에 장비 포함 가능, 제작 시스템 없음 |
| D8 | 아이템 버리기 | 반경 X칸 내 빈 타일, 없으면 "공간이 없습니다" 경고 후 취소. 휴지통 영구 삭제(task.md) | **동작 안 함**: 슬롯 밖으로 드래그해도 원래 슬롯으로 복귀(B8). 바닥 드랍은 가방 축소 시에만 발생 |
| D9 | 전리품 루팅 UX | 전투 승리/상자 오픈 시 보상 팝업(Dual Reward UI) + 미획득분 바닥 파우치 (GDD.md 2.3) | 몬스터 처치 시 개별 아이템 바닥 드랍 → 밟아서 자동 획득. 보상 팝업은 호출처 없음 |
| D10 | 드랍 아이템 안개 가시성 | `VisibleOnly`(시야 밖 은폐) (GDD.md 2.1) | `LootSystem._droppedItemFogMode` 기본값 `RememberWhenExplored` |
| D11 | 3타 콤보 / 우클릭 차징 | GDD 어디에도 명세 없음 | 구현됨 → 기획서 역반영 여부 결정 필요 |
| D12 | 몬스터 감지 | 전방 시야각(FOV) + 무기 소음 시 어그로 (Monsters.md 2) | 360도 거리 + LOS만 판정 |

---

### 2.1 유저 확정 결과 (2026-10-31)

| # | 확정 내용 |
| --- | --- |
| D1 | 가방 용량 = 기본 칸 + 가방 장비. 가방은 방어구 시스템에 종속되어 테크트리로 업그레이드 |
| D2 | 무게 시스템 완전 삭제 |
| D3 | 골드 전리품은 가방 칸을 차지하는 아이템. 허브에서 판매해야 보유 골드(지갑)로 전환 |
| D4 | 무기·방어구·가방 = 재화 소모 업그레이드로 해금 및 구매. 악세서리 = 드랍 전용 |
| D5 | 바운스 이동 연출로 확정. 슬라이딩·잔상 기획 삭제 |
| D6 | 가방(인벤토리) 오픈 시 게임 일시정지 |
| D7 | 무기·방어구·가방은 제작 전용 (던전 드랍 없음) |
| D8 | 휴지통 + 버리기 모두 지원. 아이템 우클릭 → 컨텍스트 메뉴의 '버리기' 버튼 |
| D9 | 바닥 아이템 밟아서 획득으로 고정. 상자는 상호작용 → 흔들림 등 연출 → 바닥에 드랍 |
| D10 | 보류 — 두 모드 테스트 후 결정 |
| D11 | 3타 콤보·우클릭 차징을 기획서에 반영 |
| D12 | 구현. 몬스터 데이터별로 감지 반경·감지 각도 설정 가능하게 |
| 장비 슬롯 | 무기 + 방어구 3종(투구·갑옷·신발) + 가방 + 악세서리 3종(반지·목걸이·귀걸이) = 8슬롯 |
| 탐험형 방어구 | 가방 칸 보너스 삭제. 이동속도·행동속도 강화 컨셉으로 변경 |
| 골드 전리품 | 별도 화폐 아이템이 아니라 골드 가치가 높은 일반 전리품(금이 간 찻잔, 정교한 금접시 등). 허브에서 판매 시 지갑 골드로 전환 |
| 버리기/휴지통 | 버리기 버튼은 허브에서 숨김. 휴지통은 던전·허브 인벤토리 모두. 드랍 반경은 플레이어 기준 칸 수로 인스펙터 조절 |
| 상자 | 열쇠 없음(추후 추가). 여는 입력(공격키 vs 상호작용키) 미정 |
| 퀵슬롯 | 가방의 소모품을 퀵슬롯으로 드래그해 연결(아이템은 가방에 유지) |
| 포션 수치 | 전투 중 공용 쿨타임 7초, 지속 회복 5초 (쿨타임 중 재사용 불가) |
| 포션 사용 | 퀵슬롯(1·2·3) + 인벤토리 더블클릭 모두 가능 |
| 스택 처리 | 버리기·판매·삭제 시 수량을 골라서 처리 |
| 보안 파우치 | 처음 2칸. 장비 아이템은 넣을 수 없음 |
| 휴지통 | 삭제 전 확인창 표시 |
| 장비 교체 | 장비 장착·해제는 허브에서만 가능 (던전에서는 불가) |
| 판매가 | 아이템별로 지정된 가격(baseValue) 그대로. 배율 없음 |
| 기본 장비 복구 | 잡화상에서 기본 무기·방어구·가방 판매 |
| 시작 골드 | 1000G |

---

## 3. 미구현 항목 (❌) — 우선순위 제안

### P0. 코어 전투 수치·자원 (MVP 루프 성립에 필수)
1. **데미지 공식 미적용**: 몬스터 `def`, 플레이어 `CurrentDefense`가 계산에 쓰이지 않음. `DEF/(100+DEF)` 점감, 음수 DEF 증폭(최대 2.0배) 미구현.
2. **CRIT / EVA / ArPen / LS / Thorns / Tenacity**: 스탯 필드 자체가 없음 (Player_Stats_Formulas.md).
3. **MP 사용처 없음**: MP는 표시만 됨. 소모·회복 로직 없음.
4. **전투 상태(In-Combat) 판정 없음**: `GameState { Exploration, Combat }` enum만 존재. 포션 이원화·장비 교체 잠금의 전제 조건.
5. **소모품 사용**: 포션 사용 로직, 퀵슬롯(1·2·3), 전투 중 GCD+HoT / 비전투 즉시 회복 전부 없음.
6. **장비 스킬(Q·W·E)**: `SkillDataSO`는 있으나 런타임 사용처 0. 쿨타임·MP 소모 매니저 없음.

### P1. 익스트랙션 긴장감 요소
7. **보안 파우치**: 데이터·UI·사망 시 보존 로직 모두 없음 (현재 사망 = 창고 제외 전부 증발).
8. **결과 화면**: 탈출 성공(획득 목록/가치) / 사망(Lost vs Saved 대조) 패널 없음.
9. **하단 HUD**: HP/MP는 텍스트(`PlayerStatsUIController`)만. 게이지·스킬/소모품 퀵슬롯·쿨타임 오버레이 없음.
10. **전투 중 장비 교체 잠금**: 없음 (4번에 의존).
11. **무기별 경직 차별화**: 무거운 무기 넉백/경직 없음. 현재 피격은 시각 반응만 (`HitReactionRoutine`).

### P2. 맵 오브젝트
12. **함정 동작**: 스폰만 됨. 가시 함정(최대HP 10%), 독 늪(10초 DoT) 동작 스크립트 없음.
13. **보물상자·열쇠**: 상자 프리팹 스폰만 됨. 열기/등급별 열쇠 소모/보상 UI(`DungeonUIManager.OpenRewardScreen`은 호출처 없음) 없음.
14. **제단(회복/버프/희생)**: 없음.
15. **몬스터 호버 툴팁 / ?·! 말풍선**: ?·! 텍스트 팝업은 `CombatFeedbackSystem`에 있음, 호버 툴팁·ZOC 오버레이 없음.

### P3. 허브 경제·성장
16. **상점(소모품 구매)**, **대장간(매각)**: `HubSystem`에서 로그만 출력.
17. **감정소 / 미확인 아이템**: `RuntimeItem`에 미확인 플래그·랜덤 스탯 없음.
18. **제작소(테크트리·설계도)**, **강화소(파우치 확장·스탯 제단)**: 없음.
19. **NPC 의뢰·호감도**: `NPCQuestSO`만 존재.

### P4. 무기 다양화
20. **원거리 무기 탄약/재장전**: 투사체 발사(Z키 `HandleSpecialAttack`)만 있음. 탄약 아이템·장전수·재장전 없음.
21. **마법 무기(지팡이)**: 지정 타일 지연 데미지 없음.
22. **무기 카테고리 구분**: `WeaponDataSO`에 근접/원거리/마법 타입 필드 없음.

### 추가 미구현 (Gemini 검토로 보강)
23. **몬스터 특성 AI 연동 (🟡 부분 구현)**: LIFESTEAL — 투사체 적중 시 흡혈 누락, 저체력 공격 우선권 없음 / SHIELD — 슈퍼아머(회피 스텝·기절 면제) 없음 / EXPLOSIVE — 빈사 시 자폭 돌진 없음. FAST는 구현.
24. **방어구 3분기 메커니즘**: 탐험형(가방 보너스)/방어형(이동 쿨 증가)/기동형(이동 쿨 감소·EVA). `ArmorDataSO`는 빈 클래스.
25. **악세서리 역할 분리**: 반지=액티브 고정, 목걸이/귀걸이=패시브 고정 + 랜덤 스탯, 상점 '하급 대쉬' 반지.
26. **무기 상태이상**(화상·넉백 등), **횃불 장비 시야 증가**, **봉인석/열쇠 퀘스트 오브젝트**.
27. **CSV 임포터**: `CSVImporterWindow.cs` 전체가 주석 처리됨. 해제해도 삭제된 필드(`equipType`, `bagAddedSlots`, `dropEntries`) 참조로 컴파일 불가.

### ⚪ Post-MVP (기획서상 후순위)
- 절차적 청크 생성(`ProceduralChunks` 골격만 존재), 하드모드 Heat, 귀환석·카운트다운, 바닥 파우치(Dropped Pouch), 연금술/암시장.

---

## 4. 버그·기술 부채 (코드 리뷰 중 발견)

| # | 위치 | 내용 |
| --- | --- | --- |
| B1 | `PlayerController.HandleSpecialAttack` | `PerformRangedAttack`의 `attackSpeed` 인자로 `CurrentSpeed`(이동속도)를 넘김 → 원거리 쿨다운이 이동속도에 묶임 |
| B2 | `MonsterController.DealDamageToPlayerAtCell` | `Combat.PerformMeleeAttack` 경유 → 몬스터 `EntityCombat`이 쿨다운 중이면 **데미지가 조용히 무시됨** (연속 패턴/자폭 시 누락 가능) |
| B3 | `LootSystem` | `Resources.LoadAll` 전수 로드, `new GameObject` 생성(풀링 TODO), LINQ `Sum` (체크리스트 위반) |
| B4 | `InventoryManager.InitializeBag`, `InventoryUIController`, `StorageUIController` | `FindObjectOfType<PlayerController>()` 런타임 호출 |
| B5 | `PlayerStatManager.TakeDamage` | 매 피격마다 `GetComponentInChildren<SpriteRenderer>()` |
| B6 | `MonsterDataSO.HasTrait` | 특성을 매직 스트링(`"FAST"` 등)으로 비교 → enum 전환 권장 |
| B7 | `HubSystem` / `DungeonEntranceUI` | UI가 `SceneManager.LoadScene` 직접 호출 (04_Refactoring_TODO에 기록됨) |
| B8 | `InventoryUIController.ReturnOrDropHeldItem` | `SlotIndex < 0`일 때만 바닥 드랍 → 인벤토리 아이템은 슬롯 밖으로 끌어도 버려지지 않음. 허브에서는 `LootSystem`이 없어 드랍 시 증발 위험 |
| B9 | `MonsterController.DealDamageToPlayerAtCell` | B2로 데미지가 무시돼도 `true` 반환 + LIFESTEAL 흡혈 적용 |
| B10 | `EquipmentManager.TryEquip/Unequip` | 기존 가방을 확장 슬롯에 넣은 직후 `UpdateBagCapacity(0,0)`으로 축소 → 해당 가방이 바닥으로 튕기거나 허브에서 유실 |
| B11 | `MonsterController.ExplosiveDeathRoutine` | 0.75초 퓨즈 동안 타일 점유 해제 안 됨 (경미) |
| B12 | `CameraFollow.HitStopRoutine` | 종료 시 `Time.timeScale = 1f` 강제 → 인벤토리 일시정지(D6) 도입 시 충돌 |
| B13 | 다수 스크립트 | 최상단 Role/Dependencies/Events 요약 주석 누락, `LayerMask.GetMask`/`NameToLayer` 런타임 문자열 호출 |

---

## 5. 문서 최신화 필요 (코드·현행 기획과 불일치)

| 문서 | 문제 |
| --- | --- |
| `Docs/task.md` | 구현된 항목 다수가 미체크 상태 |
| `Architecture/02_Feature_Backlog.md` | ATB 오토배틀러, 가방 테트리스, 1:1 전투 패널 등 폐기된 기획 기준 |
| `Architecture/Combat_System_Arch.md` | NavMesh 클릭 이동 + 팝업형 오토배틀러 기준 (완전히 구버전) |
| `Architecture/Inventory_System_Arch.md` | 테트리스 그리드 기준 |
| `Architecture/03_Data_Tables_Spec.md` | `GridWidth/Height`, ATB `Speed` 등 폐기 컬럼 |
| `ScriptMaps/Data_System_Map.md` | 경로 `Assets/Scripts/`(실제 `Assets/01_Scripts/`), 존재하지 않는 `CombatManager`/`DataManager` 참조 |
| `GDD/GDD.md` 3절 | `Monster_Stats.csv`의 "경험치" 언급 — 레벨/경험치 없음 규칙(Player_Stats_Formulas.md)과 모순 |
| `Architecture/04_Refactoring_TODO.md` | "EntityCombat 무기 이펙트 Instantiate" 항목은 풀링 적용으로 이미 해소 |
