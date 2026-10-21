# 스크립트 종속성 맵: 데이터 시스템 (Data System)

본 문서는 `Assets/Scripts/Data/` 폴더 하위에 위치한 스크립트들의 역할과 종속성을 한눈에 파악하기 위한 아키텍처 지도입니다.

## 📁 `Assets/Scripts/Data/Core/`
게임 전반에서 공통으로 사용되는 핵심 데이터 타입 정의.
- `GameEnums.cs`: 몬스터 타입, 스킬 카테고리 등 모든 Enum이 중앙 집중식으로 정의되어 있습니다. 게임 내 모든 매니저와 데이터 클래스가 이 스크립트를 참조합니다.

## 📁 `Assets/Scripts/Data/Models/`
엑셀(CSV) 데이터의 1행(Row)에 1:1 대응되는 `ScriptableObject` 모델 클래스들입니다.
에디터 툴(`CSVImporterWindow`)에 의해 런타임용 `.asset` 파일로 붕어빵처럼 찍혀 나옵니다.

- **`MonsterDataSO.cs`**: 몬스터의 기본 스탯과 패시브 특성(`Traits`)을 담고 있습니다. 런타임 시 `CombatManager`가 데미지를 계산할 때 이 데이터를 참조합니다.
- **`ItemDataSO.cs`**: 장비 및 소모품의 뼈대입니다. UI의 가방 테트리스 시스템(`InventoryManager`)이 크기(GridWidth/Height)를 참조합니다.
- **`SkillDataSO.cs`**: 스킬의 데미지 쿨타임 등을 정의합니다. 전투 발생 시 참조됩니다.
- **`HeatModifierSO.cs`**: 하드모드 제약 데이터를 정의합니다. `HeatManager`(위험도 관리 시스템)에서 참조합니다.

## 🔗 매핑 모델 (Mapping Models)
1:N 관계를 표현하는 데이터 구조입니다. 내부적으로 `List<T>`를 가지며 단일 객체로 저장됩니다.
- **`DropPoolSO.cs`**: 몬스터 사냥 시 전리품 로직(`LootManager`)이 참조하여 가중치(Weight) 기반으로 아이템을 드랍합니다.
- **`ItemSkillPoolSO.cs`**: 아이템 드랍 시 어떤 스킬이 붙을지 가중치 추첨을 하기 위해 참조됩니다.
- **`NPCQuestSO.cs`**: 로비 씬의 NPC 호감도 시스템이 퀘스트 요구 사항을 검사할 때 참조합니다.

---
**[종속성 흐름]**
CSV 파일 ➡️ `CSVImporterWindow` (에디터 파싱) ➡️ `Models/`의 SO 생성 ➡️ `DataManager` (캐싱) ➡️ `CombatManager`, `InventoryManager` 등 런타임 시스템이 호출
