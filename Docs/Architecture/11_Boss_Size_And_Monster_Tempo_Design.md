# 보스 크기·점유와 몬스터 이동 템포 설계안

플레이 테스트(2026-11-23)에서 나온 문제를 B3(보스 전용 패턴) 전에 정리합니다. **구현 전 설계안**입니다.

## 1. 문제

| 문제 | 원인 |
| --- | --- |
| 보스가 보스처럼 안 보임 | 멧돼지 2×2·나무 3×3이 칸 크기로 그려지는데 플레이어는 1칸 점유에 거의 2칸 높이. 카메라 Size 8로 화면에 세로 16칸이 보여 모두 작게 보임 |
| 이동 중 3×3 같지 않음 | `TileSystem.TryOccupyFootprint`가 이동 **시작 순간** 옛 칸을 풀고 새 칸을 점유. 그림은 0.38초 동안 미끄러져 따라오므로 앞줄은 그림 없이 막히고, 뒷줄은 그림이 있는데 비어 있음(통과 가능, 공격 빗나감). 보스는 이동 0.38초 + 대기 0.3초로 거의 쉬지 않아 절반 넘는 시간이 어긋난 상태 |
| 쉬지 않고 이동 | 행동 루프가 패턴 대기(`globalPatternCooldown`) 동안에도 매 반복 1칸 이동 + `moveInterval` 대기만 함 |
| 공격 애니·이펙트 방향과 피해 칸이 다름 | 별도 픽스(6절) |

## 2. 결정 사항 (유저, 2026-11-23)

| 항목 | 결정 |
| --- | --- |
| 던전 카메라 Size | 8 → **5** (세로 10칸) |
| 크기 규칙 | **5×5 = 고정형, 3×3·4×4 = 이동형** |
| 5층 멧돼지 | 2×2 → **3×3 이동형** |
| 10층 나무 | 3×3 이동형 → **5×5 고정형**. 08 문서 3절 고정형 초안(뿌리 매듭, 단계별 순서)을 B3에서 다시 살림 |
| 피격 범위 | 몸 크기 전체(점유 칸 = 피격 칸) |
| 이동 중 점유 | 개선: 이동 동안 옛 칸 + 새 칸 모두 점유, 도착 시 뒷줄 해제. **2칸 이상 몸에만** |
| 발밑 점유 표시 | 항상 옅은 테두리, 끄고 켤 수 있게. 눈으로 확인 후 유지 여부 결정 |
| 보스 그림 | 우선 PPU 배율로 키워 확인, 어색하면 새로 그림 |
| 이동 템포 | **B+C**: 멀면 몇 걸음 걷고 쉼, 패턴 사거리 안이면 대치(제자리) |

## 3. 영향 범위 (graphify + grep)

- `MonsterController` (degree 146): `Footprint`(현재 1~3 클램프), `TryMoveBody`, `RegisterBody`/`ReleaseBody`, `MoveToTileRoutine`, `StopActionCoroutines`, `BehaviorRoutine`, `FitColliderToFootprint`, `GetAnchorFromMarker`.
- `MonsterLocomotion`: `TryMoveBody` 호출 4곳(배회, HeavyChase, 후퇴, 옆걸음) + `MonsterController` 2곳(회피 스텝, 피격 후방 탈출) + `ChargePatternSO`.
- `TileSystem`: `TryOccupyFootprint`/`RegisterFootprint`/`ReleaseFootprint`.
- `MonsterDataSO.footprint` `[Range(1,3)]`, `FloorDesignValidator` 보스 몸 검증(378행 이동 스타일 경고, 398행 몸 칸 검증), `DungeonGenerator.SpawnBoss`.
- 프리팹·SO: `MossBoarBoss.asset`, `AmberTreantBoss.asset`, 두 보스 프리팹, 보스 스프라이트 PPU(멧돼지 72px/PPU 28, 나무 104px/PPU 26.67), `03_DungeonScene` Main Camera.
- `GlobalEvents` 신규 없음. 몸 칸 변경은 `MonsterController`의 지역 C# 이벤트로 발밑 표시(View)에 알림.

## 4. 설계

### 4.1 카메라
- `03_DungeonScene` Main Camera orthographic size 8 → 5. 코드가 덮어쓰지 않음, `CameraFollow`는 경계 제한 없음.
- 안개 시야 반경 4(지름 9칸)는 화면 세로 10칸 안에 들어감. 변경 없음.
- 허브(5)·타이틀(6)은 그대로.

### 4.2 크기와 고정형
- `MonsterDataSO.footprint` `[Range(1,5)]`, `MonsterController.ClampFootprint` 1~5.
- `MonsterDataSO.isStationary`(bool) 추가. 참이면:
  - `MonsterLocomotion` 이동 스텝을 건너뜀(배회 포함). 행동 루프는 패턴만 시도하고, 패턴이 없으면 짧게 대기.
  - 회피 스텝·후방 탈출 없음(크기 2 이상은 이미 없음).
  - 돌진(`ChargePatternSO`)은 `CanExecute`에서 거부.
- `FloorDesignValidator`(Validate Region Configs) 경고 추가:
  - footprint 5 이상인데 `isStationary` 아님 → "통로 5칸, 이동 중 끼임 가능"
  - `isStationary`인데 패턴에 돌진 포함
- `FloorDesignValidator.CheckBossPrefab`(378행)의 "HeavyChase로 움직입니다" 경고는 `isStationary`면 띄우지 않는다.
- 버퍼: `PatternCellsBuffer` 초기 용량 64 → 128(5×5 몸 둘레 반경 3 = 11×11 − 25 = 96칸). `ExplosiveDeathRoutine`의 `new List`도 이 버퍼를 재사용(GC 제거).
- 시야·발사 원점: `HasLineOfSightToPlayer`와 원거리 투사체는 몸 중심(`transform.position`)에서 시작한다. 5×5는 중심이 몸 가장자리에서 2.5칸 안쪽이라 몸 근처 기둥 모서리에 시야가 끊길 수 있다. 고정형 보스방에는 영향이 작고 나무에 원거리 패턴이 아직 없으므로 지금은 바꾸지 않고, 원점을 "플레이어 쪽 몸 가장자리 칸"으로 옮기는 것은 B3에서 나무 원거리 패턴(열매 폭격 등)을 만들 때 함께 정한다. 곡사(`MortarAoE`)는 시야와 무관하다.
- 짝수 크기(4×4): `GetAnchorFromMarker`의 `(size-1)/2` 오프셋으로 마커가 몸 중앙 좌하단 쪽 칸. 기존 2×2와 같은 규칙이라 변경 없음.
- 보스방(`Floor_GrayBox_Boss01`, 안쪽 22×18): B 마커 기준 5×5 몸은 기둥(2×2 4개)과 겹치지 않음. 배치 검증이 이미 몸 칸 전부를 확인.
- 데이터: 멧돼지 `footprint 3`, 나무 `footprint 5`, `isStationary true`, 나무 패턴 중 돌진 제거(B3 전까지 임시: 단일 근접·내려치기만).

### 4.3 그림 크기
- 몸 칸 대비 배율로 PPU만 바꿈: 멧돼지 28 → 18.67(2→3칸), 나무 26.67 → 16(3→5칸). 프리팹 스케일은 1 유지.
- 픽셀 밀도가 플레이어(PPU 32)의 절반 정도라 보스 픽셀이 굵게 보임. 확인 후 새로 그릴지 결정(유저).

### 4.4 이동 중 점유 (2칸 이상만)
- `TileSystem.TryOccupyFootprintKeepOld(newAnchor, size, entity)`: 새 몸 칸이 비었는지(자기 자신 제외) 확인하고 새 칸만 추가 등록. 옛 칸은 그대로 둠.
- `MonsterController`:
  - `_pendingReleaseAnchor`, `_hasPendingRelease` 필드.
  - `TryMoveBody`: 크기 2 이상이면 먼저 `FlushPendingRelease()`(남아 있는 뒷줄 정리) 후 `KeepOld`로 점유, 옛 anchor를 `_pendingReleaseAnchor`로 기억. 크기 1은 기존 `TryOccupyFootprint` 그대로.
  - `FlushPendingRelease()`: 옛 몸 칸 중 **새 몸에 속하지 않는 칸**만 `ReleaseTile`(occupant가 자신일 때만 풀리는 기존 동작 유지).
  - 호출 지점(해제를 한 함수로 모음): `MoveToTileRoutine` 도착 직후, `StopActionCoroutines`(**`_isMovingToTile` 조건과 무관하게 무조건**: `TryMoveBody` 성공 직후 `MoveToTileRoutine` 시작 전 틈의 인터럽트 대비), `ReleaseBody`(사망·자폭·`DespawnWithoutLoot`·`OnDestroy`. `_occupiedAnchor`와 함께 남은 뒷줄도 해제), `TryMoveBody` 시작. 대기 중인 해제가 없으면 아무것도 안 함(여러 번 불러도 안전).
  - 돌진은 칸마다 `TryMoveBody` → `MoveToTileRoutine`이라 칸마다 뒷줄이 풀림.
- 피격: 뒷줄도 점유 중이므로 그림이 덮은 칸을 때리면 맞음. 몸 칸 판정(`IsBodyCell`, 거리 계산)은 기존처럼 새 anchor 기준(패턴·거리는 도착 위치 기준).
- 위험: 해제 누락 시 유령 점유. 위 4개 지점 외에 `_occupiedAnchor`를 바꾸는 경로가 없는지 구현 때 grep으로 확인.

### 4.5 발밑 점유 표시 (`MonsterFootprintIndicator`, View)
- 크기 2 이상 몬스터 프리팹에 붙임. `MonsterController.OnBodyCellsChanged`(지역 이벤트: 점유 등록·이동·뒷줄 해제 때 발행)를 구독해 현재 **실제 점유 칸**(이동 중이면 옛+새 합집합)을 감싸는 사각 테두리를 그림.
- 구현: 9-slice 테두리 스프라이트 1장(`SpriteRenderer` drawMode Sliced) 크기만 조절. 몬스터 그림보다 아래 정렬, 알파 0.25 기본.
- 끄고 켜기: 컴포넌트 `[SerializeField] _visible` + 실행 중 토글. 비교 테스트용으로 단축키는 두지 않고 인스펙터에서 바꿈.
- 사망 시 숨김. `ReleaseBody()`(자폭 퓨즈 대기·강제 제거 포함)에서도 몸 칸 변경 이벤트를 발행해 그 즉시 테두리를 끈다. 안개로 몬스터가 안 보이면 같이 숨김.
- `MonsterController.Awake`의 `_renderers`(자식 `SpriteRenderer` 전부)를 `SetVisibleByFog`가 덮어쓰므로, 인디케이터 렌더러는 `_renderers`에서 제외한다. `SetVisibleByFog`가 상태를 `IsVisibleByFog` 프로퍼티로 기록하고(신규) 지역 이벤트로 알리면, 인디케이터가 그 안개 상태와 자기 `_visible`을 AND로 직접 판단한다.

### 4.6 이동 템포 B+C
`MonsterDataSO`에 추가(기본값으로 기존 몬스터 동작이 크게 변하지 않게 하고, 에셋별로 조정):

| 필드 | 의미 | 초안 |
| --- | --- | --- |
| `walkBurstSteps` | 쉬지 않고 걷는 걸음 수. 0이면 끔(기존 동작) | 일반 근접 3, 날렵 4, 보스 2 |
| `burstRestDuration` | 몰아 걷기 후 쉬는 시간(초) | 일반 0.6, 날렵 0.4, 보스 0.8 |
| `holdWhenInRange` | 패턴 대기 중 사거리 안이면 대치 | 근접·날렵·보스 true, 원거리 카이팅 false |

- **대치(C)**: 어그로 상태이고 글로벌/개별 쿨타임 때문에 패턴을 못 쓰지만, **쿨타임을 무시하면 쓸 수 있는 패턴이 하나라도 있으면**(`CanExecute` 사거리 조건 통과) 이동하지 않고 플레이어를 바라보며 0.1초 단위로 다시 판단. 대각선에서 상하좌우 전용 근접만 있는 몬스터는 사거리 밖으로 판정되어 다시 움직임.
- **몰아 걷기(B)**: 대치가 아닐 때 이동 걸음 수를 세고 `walkBurstSteps`에 도달하면 `burstRestDuration` 쉼. 패턴 사용·대치 진입·어그로 해제·인터럽트(기절·회피·후방 탈출) 진입 시 걸음 수와 `_restUntil`을 초기화.
- 쉬는 동안·대치 중에도 패턴 쿨타임이 끝나면 즉시 패턴 시도(쉬는 시간은 패턴을 막지 않음). 쉬기·대치는 `WaitForSeconds(burstRestDuration)` 통째 대기가 아니라, 쉬기 종료 시각(`_restUntil`)만 기록하고 행동 루프가 0.1초(`WaitForSeconds` 캐싱) 단위로 돌며 매번 패턴 추첨을 먼저 시도한다. 쉬는 중이면 이동만 건너뛴다.
- 걸음 수는 **실제로 몸이 옮겨졌을 때만** 센다. `ExecuteMoveStepRoutine`은 막힘·배회 확률 실패로 제자리일 수 있으므로, `TryMoveBody` 성공 때 증가하는 `MonsterController._successfulMoveCount`를 이동 스텝 전후로 비교한다(로코모션 시그니처 변경 없음).
- 대치 중에는 0.1초마다 바라보는 방향을 플레이어 쪽으로 갱신한다. 지금 `MonsterFacingVisual`은 이동 중이거나 패턴 시작(`ActionCount`) 때만 방향을 바꾸므로, 대치 루프에서 시선 갱신용 프로퍼티(`LookTargetDirection`)를 설정하고 Visual이 이를 읽게 한다(패턴 카운트 `MarkActionTowardPlayer`는 공격 애니를 트리거할 수 있어 쓰지 않음).
- 고정형(`isStationary`)은 이동 스텝이 없으므로 몰아 걷기·대치 판정을 건너뛰고 항상 제자리에서 플레이어를 바라본다.
- 배회(Patrol)는 기존 그대로(40% 확률·1.4배 간격).
- 원거리 카이팅은 거리 유지가 정체성이라 `holdWhenInRange false`, 몰아 걷기만 적용.
- 이동 1칸 시간(`moveDuration`)은 데이터로 조정. 보스는 0.38 → 0.25를 초안으로 하고 플레이 테스트로 정함(짧을수록 4.4절 어긋남 시간도 짧아짐).

## 5. 구현 단위·PR

| 단위 | 내용 | 브랜치·PR |
| --- | --- | --- |
| F1 | 카메라 Size 5, 공격 방향 버그(6절) | `fix/camera-attack-dir` |
| S1 | 크기 상한 5, `isStationary`, 이동 중 점유, 발밑 표시, 보스 데이터·PPU, 검증 경고 | `feat/boss-size` |
| T1 | 이동 템포 B+C, 몬스터 SO 수치 | `feat/monster-tempo` |
| B3 | 보스 전용 패턴(나무 = 08 고정형 초안, 멧돼지 3×3 기준 재조정) | 이후 |

각 단위는 develop에서 브랜치. PR #52(B2) 병합 후 시작.

## 6. 별도 픽스: 공격 방향
- 원인: 피해 칸은 클릭 칸(`targetCellPos`), 애니·이펙트는 `_facingDirection`. `HandleAimInput`이 `Attacking` 중 조준을 무시하고, 선입력 공격은 공격 종료 프레임에 바로 이어져 `_facingDirection`이 갱신될 틈이 없음.
- 수정: `ExecuteMeleeComboAttack` 시작 때 **사거리 보정(329행)보다 먼저** 클릭한 원래 대상 칸 − 내 칸으로 `_facingDirection`을 갱신(8방향 스냅)하고, 그다음 사거리 밖이면 갱신된 방향으로 인접 칸을 고른다. 차징 공격(`ExecuteChargedLineAttack`)과 원거리(`HandleSpecialAttack`)는 시작·발사 시점의 현재 마우스 조준 방향으로 다시 계산한다.

## 8. 검토 기록
- Gemini 코드 설계 검토 1차 (gemini-4-argon-medium, 2026-11-23): CHANGES_REQUESTED. MAJOR 3건(뒷줄 해제 지점 보강, 쉬기·대치 폴링과 실제 이동 기준 걸음 수, footprint 5 확장 영향: 버퍼·검증 경고·시야 원점) 반영. MINOR 3건(대치 중 시선, 인디케이터와 안개 렌더러 분리, 공격 방향 보정 순서) 반영. [IDEA] 1건(자폭 버퍼 재사용)은 4.2절에 함께 반영.
- 2차 (같은 모델): APPROVED. MINOR 2건(어그로 해제·인터럽트 때 걸음 수 초기화, `ReleaseBody` 때 발밑 표시 끄기) 4.5·4.6절에 반영. 토큰 합계 1차 116,851 + 2차 71,341 = 188,192.

## 7. 테스트
- Claude: 컴파일·콘솔, Validate Region Configs(5×5 고정 나무 배치, 새 경고).
- 유저 플레이 체크리스트: 멧돼지 이동 중 뒷줄 통과 불가·피격, 기절·사망 후 유령 점유 없음, 나무 고정, 발밑 표시 켜고 끈 비교, 일반 몬스터 대치·몰아 걷기 느낌, 카메라 5에서 원거리 몬스터가 화면 밖에서 쏘지 않는지.
