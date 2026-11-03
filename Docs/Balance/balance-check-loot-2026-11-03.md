# 밸런스 점검: 전리품·드랍 (loot)

- 날짜: 2026-11-03
- 브랜치: `feat/floor-t3` (커밋하지 않은 변경 포함 상태에서 읽음)
- 도구: `/my-skills:balance-check loot`

## 데이터 출처
- `Assets/Resources/Data_SO/DropPoolSO/` (DropPool_Chest_Region1, NewDropPool 1)
- `Assets/Resources/Data_SO/ItemSO/` (70개)
- `Assets/Resources/Data_SO/MonsterSO/` (AgileMonster, MelleMonster, WizardMonster)
- `Assets/Resources/Data_SO/FloorSO/Region1/Region1Config.asset`
- `Assets/Resources/Prefabs/Player.prefab` (가방·보안 파우치 칸)
- `Assets/01_Scripts/Combat/LootSystem.cs` (굴림 방식, 던전 드랍 제한)
- `Docs/GDD/Items.md`, `Docs/Architecture/07_Floor_Based_Dungeon_Design.md`

## 종합: 주의

남은 테스트 값과 문서-데이터 불일치가 있다. 드랍량 목표가 문서에 없어 이상치 판정은 일부 평가 불가.

## 문서-데이터 불일치
| 항목 | 문서 (위치) | 데이터 (파일) | 비고 |
|---|---|---|---|
| 가방 기본 칸 | 30 (Items.md 1. 가방) | 35 (Player.prefab `_baseMaxSlots`) | 코드 기본값은 30 |
| 골드 | "몬스터가 떨어뜨리는 재화 아이템" (Items.md 2. 아이템 분류) | 드랍 풀 골드 0~0 (NewDropPool 1 `minGold`/`maxGold`), 몬스터 `goldDropBase` 12/15/18 | 골드가 어느 경로로 지급되는지 확인 필요 |

## 이상치
| 항목 | 목표 범위 (출처) | 실제 값 (출처) | 문제 |
|---|---|---|---|
| 몬스터 드랍 장비 | 던전 드랍은 악세서리만 (LootSystem.cs `WarnIfCraftOnly`) | Backpack·Sword 확정, Armor·Helmet·Backpack 랜덤 (NewDropPool 1) | 규칙 위반. 실행 시 경고 로그 |

## 지배 전략·깨진 진행
- **Rare 22종, Legendary 11종은 얻을 경로가 없다.** 몬스터는 모두 `dropPoolID: 0`(NewDropPool 1)을 쓰고 이 풀에는 전리품이 없다. 지역 상자 풀(id 1000)에는 몬스터별 Common 2종(xx1, xx2)만 있다.

## 계산 결과

### 몬스터 처치 1회 (NewDropPool 1, 3종 공통)
- 확정: crab(1) 1~2, HP포션 1~4, MP포션 0~3, Basic Backpack 1, Basic Sword 1
  - 평균 개수 = 1.5 + 2.5 + 1.5 + 1 + 1 = 7.5
- 랜덤 `Equip` 그룹: 굴림 횟수 = floor(Lerp(1, 5.99, curve(u)))
  - 커브 키 (0,0) (0.857,0.342) (1,1)을 직선으로 근사 (탄젠트 미반영): 1회 50.2%, 2회 36.8%, 3회 4.3%, 4회 4.4%, 5회 4.3% → 기댓값 약 1.76회
  - 1회당 당첨 확률 = 25 / 45 (꽝 가중치 20) → 장비 약 0.98개
- **처치당 약 8.5개, 그중 장비(겹치지 않음) 약 3.0칸**

### 가방 압박 (현재 데이터 기준)
- 1층 몬스터 4마리 (07 문서 층 규모 표) → 장비 약 12칸 + crab 6개(2칸) + HP포션 10개(2칸) + MP포션 6개(2칸) ≈ **18칸**
- 가방 35칸 (Player.prefab) → **2층에서 가득 찬다**

### 상자 1개 (DropPool_Chest_Region1)
- `재료 2 확정`: 2회 고정, 22종 균등 → Common 재료 2개
- `30% 장비 또는 포션`: 1회, 악세서리(Necklace·Ring·Earring) 18%, HP·MP포션 12%, 꽝 70%
- 층별 상자 기댓값: 1~4층 0.4개, 6~9층 0.8개 (Region1Config `_floorRules`, 07 문서와 일치)

## 남은 테스트 값
- 몬스터 3종 `dropPoolID: 0` → 테스트 풀 `NewDropPool 1` (MonsterSO/*.asset)
- 전리품 55종 `baseValue: 0` (ItemSO/LootSO)
- `_debugAllowDuplicateDesigns: 1` (Region1Config)
- `_midBossFloor`와 `_bossFloor`가 같은 디자인 (FloorDesign_GrayBox_Boss01)

## 제안
| 우선순위 | 문제 | 제안 | 영향 |
|---|---|---|---|
| 1 | 몬스터가 테스트 풀을 씀 | 몬스터별 드랍 풀을 만들고 각 몬스터 전리품 5종(Common 2 / Rare 2 / Legendary 1)을 등급 가중치로 넣는다. 확정 장비는 뺀다 | 가방 압박, Rare·Legendary 획득 경로 |
| 2 | 드랍량 목표가 없음 | 07 문서에 "층당 획득 칸 수"와 "가방이 차기 시작하는 층" 목표를 정한다 | 다음 점검에서 이상치 판정 가능 |
| 3 | 가방 칸 불일치 | 30과 35 중 하나로 정하고 Items.md 또는 Player.prefab을 맞춘다 | 가방 압박 계산의 기준 |
| 4 | Legendary maxStack 10 | 등급별 maxStack을 둘지 정한다 | 칸 압박 |

## 평가하지 못한 항목
- 층당 드랍 개수 목표: GDD와 07 문서에 없음. 07 문서 "층 규모" 표 근처에 있어야 함
- 가방이 차기 시작해야 하는 층: 위와 같음
- 경제(판매가): `baseValue`가 전부 0
