# 🧠 고급 몬스터 AI 아키텍처 및 설계 철학 (Portfolio & Retrospective)

> **문서 목적**: 포트폴리오 기술 서술 및 아키텍처 의사결정 회고록  
> **상세 구현 명세**: [Docs/GDD/Monster_AI.md](file:///c:/Users/jdk95/Desktop/100BagBack/Docs/GDD/Monster_AI.md)

---

## 1. 아키텍처 전환 배경: 왜 개별 FSM에서 '조립식 SO 패턴'으로 바꾸었는가?

### 🔴 기존 방식의 한계 (Problem)
1. **비주얼 리소스 제약 대비 비효율적인 코드 파편화**:
   * 1인 개발 환경에서 몬스터 아트가 프레임 애니메이션이 없는 **단일 스프라이트(Single Sprite)**로 구성됨.
   * 외형은 단일 스프라이트인데 몬스터마다 개별 FSM 상태 클래스(`MeleeState`, `RangedBrain` 등)를 하드코딩할 경우, 시각적 산출물 대비 스크립트 유지보수 비용(Class Explosion)이 기하급수적으로 폭증함.
2. **이동과 공격의 강결합**:
   * 공격 쿨타임 동안 몬스터가 이동하지 못하고 제자리에 멈춰 서 있거나, 신규 몬스터 바리에이션을 만들 때마다 C# 스크립트를 재작성해야 하는 병목 발생.

### 🟢 해결 아키텍처 (Solution)
1. **액션 주체의 공간화 (Spatial Telegraphing)**:
   * 스프라이트 모션 대신 **'타일 장판(`AttackWarningUI`), 회전형 레이저 조준선(`LineRenderer`), 돌진 물리 궤적'**으로 액션의 중심축을 그리드 공간으로 이전.
2. **ScriptableObject 기반 조립식 레고 블록 (`MonsterPatternSO`)**:
   * 공격 패턴을 몬스터 본체([MonsterController.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/MonsterController.cs))에서 완전히 분리하여 원자적 SO 에셋으로 캡슐화.
   * 기획자는 [MonsterDataSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Data/Models/MonsterDataSO.cs)의 `patterns` 리스트에 원하는 패턴 블록을 드래그 & 드롭하고 가중치(`weight`)만 설정하면 **가중치 랜덤(Weighted Random) 추첨기**가 사거리 조건에 맞는 패턴을 자동 실행.
3. **이동 템포와 행동 쿨타임의 분리**:
   * `globalPatternCooldown`이 도는 동안에도 [MonsterLocomotion.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/MonsterLocomotion.cs)가 `HeavyChase`(직진), `AgileFlank`(측면 우회), `RangedKiting`(거리 유지) 이동을 끊임없이 수행.

---

## 2. 핵심 엔지니어링 성과 (Key Engineering Highlights)

* **완벽한 OCP(개방-폐쇄 원칙) 달성**:
  * 6종의 코어 패턴 블록([SingleMeleePatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/SingleMeleePatternSO.cs), [AoESlamPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/AoESlamPatternSO.cs), [ChargePatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/ChargePatternSO.cs), [HitAndRunPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/HitAndRunPatternSO.cs), [RangedProjectilePatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/RangedProjectilePatternSO.cs), [MortarAoEPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/MortarAoEPatternSO.cs))의 파라미터 조합만으로 코드 재컴파일 없이 수십 종의 몬스터 즉각 생성.
* **타일 제약과 자유 각도의 조화**:
  * 근거리/착탄형 공격은 격자 타일 장판(`AttackWarningUI` + Danger Map A* 비용 연동)으로 명확한 대피 공간을 제시하고, 원거리 직사 투사체는 임의 각도 회전이 가능한 레이저 조준선(`LineRenderer`)으로 시각적 오염 없이 궤적을 예고.
* **억까 없는 공정한 수 싸움 (Exploitable Hard AI)**:
  * 돌진 패턴 유도 후 벽 충돌 시 1.5초 기절(Stun) 프리딜, 벽 몰이 시 측면 회피 차단, 모든 패턴 종료 후 명확한 후딜레이(`recoveryDuration`) 보장으로 타일 이동 제약 속에서도 납득 가능한 전투 쾌감 완성.
