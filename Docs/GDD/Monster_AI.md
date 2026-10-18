# 🧠 몬스터 인공지능 통합 명세서 (Monster AI Architecture & Specs)

> **문서 목적**: 게임 내 모든 몬스터 AI의 기획적 행동 원리와 SO 기반 조립식(Composable) 아키텍처를 정의하는 단일 공식 통합 명세서.  
> **상태**: 구현 완료 (`Assets/01_Scripts/Monster/`)  
> **관련 문서**: [Monsters.md](file:///c:/Users/jdk95/Desktop/100BagBack/Docs/GDD/Monsters.md), [Advanced_Monster_AI_Architecture.md](file:///c:/Users/jdk95/Desktop/100BagBack/Docs/GDD/Advanced_Monster_AI_Architecture.md)

---

## 1. 아키텍처 전환 배경 및 핵심 설계 철학

### 1.1 왜 개별 FSM 코딩에서 'SO 기반 조립식 패턴(Composable Pattern)'으로 전환했는가?
1. **비주얼 제약(단일 스프라이트)의 엔지니어링적 재해석**:
   * 1인 개발 환경상 몬스터가 프레임 애니메이션이 없는 '단일 스프라이트'로 구성됨.
   * 공격의 시각적 피드백을 몬스터 몸체 애니메이션이 아닌 **'그리드 타일 장판(Telegraph), 레이저 조준선, 돌진 궤적, 탄막'**이라는 공간 기믹으로 치환.
   * 따라서 공격 로직은 몬스터 본체(`MonsterController`)에 종속될 필요가 없으며, 그리드 공간과 상호작용하는 **독립된 패턴 레고 블록(`MonsterPatternSO`)**으로 격리하는 것이 가장 합리적임.
2. **클래스 폭발(Class Explosion) 방지 및 SRP/OCP 준수**:
   * 몬스터마다 C# 클래스를 새로 코딩하는 자원 낭비를 배제.
   * `MonsterController`는 오직 **이동 템포 실행**과 **가중치 랜덤 패턴 추첨기(Runner)** 역할만 수행하고, 구체적인 기술은 `MonsterPatternSO` 파생 에셋들을 인스펙터에서 드래그 & 드롭으로 조립하여 **코드 수정 0줄로 신규 몬스터를 무한 생성(OCP)**함.

---

## 2. 이동 템포(Locomotion)와 행동 쿨타임(Action Rhythm)의 분리

몬스터가 공격 쿨타임 동안 제자리에 멍하니 멈춰 있는 현상을 방지하기 위해 이동과 패턴 쿨타임을 완전히 분리한다.

* **이동 템포 (`MonsterLocomotion.cs`)**:
  * `moveDuration`: 타일 1칸 이동에 걸리는 시간(초)
  * `moveInterval`: 1칸 이동 후 다음 걸음까지의 대기 시간(초)
  * `movementStyle`:
    1. `HeavyChase` (묵직한 직진 추적: A* 최단+위험우회 경로로 우직하게 전진)
    2. `AgileFlank` (날렵한 측면 우회: 플레이어의 정면 시선을 피하고 측면/후방 인접 타일 선점)
    3. `RangedKiting` (원거리 거리 유지: 적정 사거리 유지, 접근 시 후퇴/코너 횡이동, 적정 거리 시 스트레이핑)
* **행동 패턴 템포 (`MonsterController.cs`)**:
  * `globalPatternCooldown`: 패턴 시전 후 다음 패턴 추첨까지의 대기 시간. **이 쿨타임이 도는 동안에도 몬스터는 멈추지 않고 `movementStyle`에 따라 이동을 수행한다.**
  * **가중치 랜덤 채택 (Weighted Random Selection)**:
    * 우선순위 고정 방식이 아니라, 현재 거리 조건(`minRange` ~ `maxRange`)과 개별 쿨타임(`patternCooldown`)을 만족하는 후보 패턴들의 `weight` 합계를 구해 **확률 비례 주사위 추첨**으로 다음 패턴을 결정한다.

---

## 3. 공격 유형별 전조(Telegraph) 및 공정성(억까 방지) 규칙

| 공격 분류 | 전조(Telegraph) 표시 방식 | 각도/판정 처리 | 유저 파훼법 (딜타임) |
| :--- | :--- | :--- | :--- |
| **근거리 타격 / 광역 슬램**<br>(`SingleMelee`, `AoESlam`) | 바닥 타일 빨간 장판 (`AttackWarningUI`) | 격자 타일 일치 | `telegraphDuration` 내 장판 밖으로 이동 후 `recoveryDuration` 동안 반격 |
| **직선 돌진**<br>(`ChargePatternSO`) | 돌진 경로 직선 타일 장판 예고 | 4방향 직선 격자 돌파 | 옆 칸으로 회피하여 몬스터를 **벽에 충돌시키면 1.5초 기절(Stun) 프리딜** |
| **원거리 직사 투사체**<br>(`RangedProjectilePatternSO`) | 바닥 장판 ❌<br>**자유 각도 회전 레이저 조준선 (`LineRenderer`)** | 45도가 아닌 임의 각도(단발/예측샷/부채꼴 확산탄) 완벽 대응 | 레이저 조준선 밖으로 1칸 비켜서거나 날아오는 탄환 보고 회피 |
| **원거리 곡사 포격**<br>(`MortarAoEPatternSO`) | 비행 궤적 장판 ❌<br>**최종 착탄 타일(Landing Cell)에만 장판** | 플레이어 이동 방향 예측 착탄 | 착탄 예고 타일에서 이탈 (Area Denial 회피) |

---

## 4. 스크립트 폴더 구조 (`Assets/01_Scripts/Monster/`)

* [MonsterController.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/MonsterController.cs): 몬스터 본체 생명주기, 이동/패턴 루프 실행기, 가중치 랜덤 추첨, 레이저 조준선 풀링, 반응형 회피/기절 제어
* [MonsterLocomotion.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/MonsterLocomotion.cs): 3대 이동 스타일(`HeavyChase`, `AgileFlank`, `RangedKiting`) 및 A* 타일 이동 헬퍼
* `Patterns/` (조립식 레고 블록 SO):
  * [MonsterPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/MonsterPatternSO.cs): 패턴 추상 베이스 클래스
  * [SingleMeleePatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/SingleMeleePatternSO.cs): 단일 근접 타격 (강타 / 찌르기)
  * [AoESlamPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/AoESlamPatternSO.cs): 광역 장판 내려치기 (3x3 정사각형 / 확산 마름모 / 전방 박스)
  * [ChargePatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/ChargePatternSO.cs): 직선 돌진 및 벽 충돌 기절(Stun)
  * [HitAndRunPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/HitAndRunPatternSO.cs): 기습 타격 후 즉각 후방 백스텝 이탈
  * [RangedProjectilePatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/RangedProjectilePatternSO.cs): 원거리 직사 투사체 (단발 / 예측 사격 / 부채꼴 확산탄 + 레이저 조준선)
  * [MortarAoEPatternSO.cs](file:///c:/Users/jdk95/Desktop/100BagBack/Assets/01_Scripts/Monster/Patterns/MortarAoEPatternSO.cs): 원거리 곡사 착탄 장판 포격
