# balance-check 설정 (100BagBack)

git으로 공유된다. 보고서와 프롬프트 같은 작업 파일(`.claude/review/`)은 커밋하지 않는다.

## 데이터 폴더
| 영역 | 경로 | 비고 |
|---|---|---|
| 몬스터 | Assets/Resources/Data_SO/MonsterSO | 패턴 SO는 `MonsterSO/PatternSO` |
| 아이템 | Assets/Resources/Data_SO/ItemSO | EquipmentSO, KeySO, LootSO(몬스터별 하위 폴더) 등 |
| 드랍 | Assets/Resources/Data_SO/DropPoolSO | 몬스터는 `dropPoolID`, 지역 상자는 `_chestDropPoolId`로 연결 |
| 층·지역 | Assets/Resources/Data_SO/FloorSO | `RegionNConfig`의 `_floorRules`는 층 순서대로 |
| 상점 | Assets/Resources/Data_SO/ShopSO | GeneralStoreCatalog |

## enum 의미 (Assets/01_Scripts/Data/Core/GameEnums.cs)
| 필드 | 값 |
|---|---|
| MonsterDataSO.type | 0 Normal, 1 Elite, 2 Boss |
| MonsterDataSO.movementStyle | 0 HeavyChase, 1 AgileFlank, 2 RangedKiting |
| ItemDataSO.category | 0 Equipment, 1 Consumable, 2 Material, 3 Key, 4 Valuable |
| ItemDataSO.rarity | 0 Common, 1 Rare, 2 Legendary |
| FloorDesignSO._band | 0 Early(1~5층), 1 Late(6~9층), 2 Boss(5층 중간 보스 / 10층 보스) |

## 코드·프리팹에 있는 수치
코드 기본값과 프리팹 값이 다른 것이 있다. **프리팹 값이 실제 값이다.**

| 값 | 코드 (기본값) | 프리팹 `Assets/Resources/Prefabs/Player.prefab` |
|---|---|---|
| HP / MP / 공격력 / 방어력 | PlayerStatManager `_baseHp` 100 / `_baseMp` 50 / `_baseAttack` 10 / `_baseDefense` 5 | 같음 |
| 이동 속도 | PlayerStatManager `_baseSpeed` 10 | 8 |
| 공격 속도 (초당) | PlayerStatManager `_baseAttackSpeed` 1 | 1 |
| 칸당 이동 시간 | PlayerMovement `_moveDurationPerTile` 0.3 | 0.3 |
| 가방 기본 칸 | InventoryManager `_baseMaxSlots` 30 | 35 |
| 보안 파우치 칸 | SecurePouchManager `_baseSlots` 2 | 2 |
| 차징 배율 / 차징 시간 / 차징 사거리 | PlayerController `_chargeDamageMultiplier` 2.0 / `_chargeDuration` 0.6 / `_chargeLineLength` 2 | 같음 |
| 콤보 배율 (1.0/1.15/1.5) | 위치 미확인 | 미확인 |
| 시작 골드 | SaveData `StartingGold` 1000 (const) | 해당 없음 |

프리팹 값이 바뀌었을 수 있으니 점검할 때마다 `Player.prefab`을 grep해서 다시 확인한다. 씬에서 덮어쓴 값은 unityMCP로 확인한다.

## 목표 수치가 있는 문서
| 영역 | 문서 | 절 |
|---|---|---|
| 전투 | Docs/GDD/Combat.md | 2.3 공격, 2.4 무기별 차이, 3. 플레이어 스탯 |
| 몬스터 | Docs/GDD/Monsters.md | 1. 등급, 4. 구현된 몬스터 |
| 아이템·가방 | Docs/GDD/Items.md | 1. 가방, 2. 아이템 분류, 5. 사망과 생환 |
| 던전·층 | Docs/GDD/Dungeon.md, Docs/Architecture/07_Floor_Based_Dungeon_Design.md | 맵 구성, 배치 규칙, 탈출과 사망 |
| 전체 | Docs/GDD/GDD.md, Docs/GDD/Roadmap.md | |

과거 밸런스 토론의 전제(`.claude/review/balance_prompt*.txt`)는 **목표로 쓰지 않는다.** 당시 수치라서 지금 데이터와 다를 수 있다. 참고로만 읽는다.

## 보고서 저장 위치
`Docs/Balance/` (2026-11-03 유저 결정)

## 검토 연동
`gemini-reviewer` 서브에이전트에 **"기획 검토"**로 요청한다. 대상 파일로 저장한 보고서 경로를 준다. 규칙은 CLAUDE.md의 "Gemini 검토 규칙"을 따른다.

## 프로젝트 규칙 메모
- 수치 변경은 Echo & Verify를 거친다. 유저가 "진행하라"고 하기 전에는 SO나 프리팹을 고치지 않는다.
- 기획 결정을 바꾸는 수치 변경은 `Docs/GDD/` 해당 문서도 같이 고친다 (Agent Rules 5. 문서화 전략).
- SO나 프리팹 값은 unityMCP로 바꾸는 것이 기본이다 (CLAUDE.md 4단계).
