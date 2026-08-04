# 데이터 테이블 명세서 (Data Tables Specification)

본 문서는 게임 내 모든 정적 데이터(엑셀/CSV)의 뼈대와 컬럼 구조를 정의합니다. 기획자는 이 명세서의 컬럼 포맷에 맞추어 엑셀 데이터를 작성해야 하며, 엔진(C#)은 본 포맷을 기준으로 파싱(`DataManager`)을 수행합니다.

## 1. ID 명명 규칙 (ID Naming Convention)
효율성과 기획 가독성의 타협점(하이브리드)을 적용하여 데이터별 ID 타입을 엄격히 분리합니다.

*   **정수형(int) ID 사용 (아이템/몬스터 등)**: `1001`, `2001`
    *   데이터의 개수가 많아 넘버링 카테고리(예: 1000번대 몹, 2000번대 장비)로 분류하는 것이 관리하기 편한 경우 사용.
    *   탐색 성능(Dictionary)에 미세한 이점이 있음.
*   **문자열(string) ID 사용 (스킬/특성/버프 등)**: `SKILL_STUN`, `LIFESTEAL`
    *   종류가 제한적이며, 매핑 테이블에서 다른 기획자가 보았을 때 즉각적으로 용도를 파악해야 하는(휴먼 에러 방지) 경우 강력히 권장.

---

## 2. 핵심 데이터 테이블 구조

### 2.1 `Monster_Stats` (몬스터 스탯 데이터)
전투 및 탐험 로직의 핵심이 되는 몬스터 기본 정보입니다.
*   **MonsterID** (`int`): 몬스터 고유 식별자 (예: 1001)
*   **Name** (`string`): 몬스터 이름
*   **Type** (`enum`): `Normal`, `Elite`, `Boss`
*   **HP** (`int`): 기본 체력
*   **ATK** (`int`): 공격력
*   **DEF** (`int`): 방어력 (피해 감소율 산정용)
*   **Speed** (`float`): 오토배틀러 행동 게이지 충전 속도
*   **Range** (`int`): 공격 사거리 (타일 단위)
*   **ZOC_Radius** (`int`): 스텔스 감지 시야 반경
*   **Traits** (`string`): 패시브 특성 리스트 (쉼표 분리. 예: `LIFESTEAL,REFLECT`. 기본몹은 `None`)
*   **DropPoolID** (`int`): 몬스터 처치 시 전리품을 추첨할 공용 드랍 풀 ID
*   **GoldDropBase** (`int`): 처치 시 지급되는 기본 골드량

### 2.2 `Item_Stats` (아이템 및 장비 데이터)
테트리스 규격과 장비/소모품의 기본 속성을 정의합니다.
*   **ItemID** (`int`): 아이템 고유 식별자 (예: 2001)
*   **Name** (`string`): 아이템 이름
*   **Category** (`enum`): `Equipment`, `Consumable`, `Material`, `Key`
*   **EquipType** (`enum`): `Weapon`, `Armor`, `Accessory`, `Backpack`, `None`
*   **GridWidth** (`int`): 테트리스 가로 차지 칸 수 (예: 1, 2)
*   **GridHeight** (`int`): 테트리스 세로 차지 칸 수 (예: 1, 2)
*   **BaseValue** (`int`): 기본 골드 가치 (상점 매각가 산정 등)
*   **Rarity** (`enum`): `Common`, `Rare`, `Legendary`

### 2.3 `Skill_Stats` (장비 종속형 스킬 데이터)
액티브/패시브 스킬의 제원입니다.
*   **SkillID** (`string`): 스킬 고유 ID (예: `SKILL_STUN`)
*   **Name** (`string`): 스킬 이름
*   **ActivationType** (`enum`): `Active` (수동 발동), `Passive` (상시/조건부 발동)
*   **RoleType** (`enum`): `Interrupt`, `Survival`, `Execution` 등
*   **MPCost** (`int`): 발동 시 소모 마나
*   **Cooldown** (`float`): 자체 재사용 대기 시간(초)
*   **EffectValue** (`float`): 스킬 위력 (타격 배율, 회복량 등)
*   **Duration** (`float`): 효과 지속 시간 (즉발은 0)

---

## 3. 정규화(매핑) 테이블 구조
1:N 관계를 깔끔하게 처리하기 위한 분리된 엑셀 시트들입니다.

### 3.1 `Item_Skill_Pool` (아이템 ↔ 스킬 확률 매핑)
장비 획득/감정 시 부여될 확률적 스킬 리스트를 정의합니다.
*   **ItemID** (`int`): 대상 아이템 ID
*   **SkillID** (`string`): 부여될 수 있는 스킬 ID
*   **RollWeight** (`int`): 추첨 확률 가중치

### 3.2 `Drop_Pool_Table` (공용 드랍 풀)
몬스터마다 똑같은 아이템 리스트를 여러 번 작성하는 것을 방지합니다.
*   **DropPoolID** (`int`): 공용 풀 ID (예: 1=잡몹공용)
*   **ItemID** (`int`): 드랍될 수 있는 아이템 ID
*   **DropWeight** (`int`): 드랍 확률 가중치

---

## 4. 부가 시스템 테이블

### 4.1 `NPC_Quest_Table` (NPC 호감도 의뢰 데이터)
가방 공간을 압박하고 NPC 기능을 해금하기 위한 의뢰 목록입니다.
*   **QuestID** (`int`): 퀘스트 고유 식별자
*   **NpcID** (`string`): 의뢰를 주는 NPC ID (예: `NPC_WEAVER`)
*   **ReqItemID** (`int`): 요구 아이템 ID
*   **ReqAmount** (`int`): 요구 개수
*   **RewardAffinity** (`int`): 완료 시 획득 호감도

### 4.2 `Heat_Modifiers` (하드모드 제약 데이터)
위험도(Heat) 체크 시 적용되는 페널티 테이블입니다.
*   **ModifierID** (`string`): 제약 고유 ID (예: `HEAT_IRONSKIN`)
*   **Name** (`string`): 제약 이름
*   **HeatScore** (`int`): 틱박스 체크 시 상승하는 위험도 점수
*   **TargetStat** (`string`): 변경 대상 스탯 (예: `EnemyDEF`)
*   **Value** (`float`): 증감 수치 배율 (예: `+0.3`)
