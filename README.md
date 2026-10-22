# 100 Bags Back

기지에서 가방을 챙겨 던전에 들어가 전리품을 모으고, 탈출 포탈까지 살아서 돌아오면 전리품을 지키는 **익스트랙션 실시간 타일 액션 RPG**입니다.
던전에서 죽으면 가져간 아이템을 잃기 때문에, 칸 수와 무게가 제한된 가방을 떠안고 언제 돌아갈지 판단하는 것이 핵심입니다.

![플레이 화면](Docs/Images/gameplay.gif)

▶ [전체 플레이 영상 보기 (Google Drive)](https://drive.google.com/file/d/1jH1GM-hGB8VthlJEk8iTCB-2Uvn_SG6Y/view)

- **개발 형태**: 1인 개인 프로젝트 (기획·개발, 진행 중)
- **엔진**: Unity 2022.3.62f3 (2D), C#

## 실행 방법

1. Unity Hub에서 **Unity 2022.3.62f3**으로 이 프로젝트 폴더를 엽니다.
2. `Assets/Scenes/02_HubScene.unity`를 열고 Play를 누릅니다.
3. 기지에서 던전 입구를 클릭하면 `03_DungeonScene`으로 이동합니다. 탈출 포탈에 도착하거나 사망하면 기지로 돌아옵니다.

> 재배포가 금지된 외부 에셋은 레포에 포함하지 않았습니다. 아래 [외부 에셋](#외부-에셋)을 받아 지정된 폴더에 넣어야 UI와 이펙트가 정상적으로 보입니다.

## 외부 에셋

**레포에 포함하지 않은 에셋** (라이선스상 재배포 금지, 직접 받아서 넣어야 함)

| 에셋 | 출처 | 넣을 위치 |
| --- | --- | --- |
| GUI Parts (PONETI) | [Unity Asset Store](https://assetstore.unity.com/packages/2d/gui/icons/gui-parts-159068) | `Assets/Resources/Sprites/GUI_Parts/` |
| Effect and FX Pixel (Free) (bdragon1727) | [itch.io](https://bdragon1727.itch.io/750-effect-and-fx-pixel-all) | `Assets/Resources/Effect/Effect and FX Pixel All Free/` |
| 48x48 Shader Slash (Free) (bdragon1727) | [itch.io](https://bdragon1727.itch.io/250-pixel-shader-slash) | `Assets/Resources/Effect/48x48 Shader Slash Free/` |

**레포에 포함된 에셋** (CC0)

- [Kenney](https://www.kenney.nl) — Tiny Dungeon, Isometric Miniature Dungeon / Farm / Library

## 조작법

| 입력 | 동작 |
| --- | --- |
| `W` `A` `S` `D` / 방향키 | 타일 1칸씩 이동 |
| 마우스 이동 | 조준 (8방향) |
| 마우스 왼쪽 클릭 | 근접 공격 (3타 콤보) |
| 마우스 오른쪽 길게 누르기 → 떼기 | 차징 강공격 (전방 직선) |
| `Tab` | 인벤토리 열기 / 닫기 |
| `Esc` | 인벤토리 닫기 |
| `E` | 인벤토리 ↔ 창고 빠른 이동 |

이동 중에는 공격할 수 없습니다. 이동과 공격 중 하나를 고르게 하는 것이 전투 설계의 핵심이며, 행동 중에 누른 입력은 행동이 끝나는 순간 이어서 실행됩니다(선입력).

## 핵심 시스템

| 시스템 | 한 줄 설명 | 주요 코드 |
| --- | --- | --- |
| 몬스터 AI 패턴 | 공격 패턴을 ScriptableObject로 분리해 조합하고, 가중치 랜덤으로 패턴을 고릅니다. 모든 공격은 바닥 장판으로 먼저 예고합니다. | [MonsterController.cs](Assets/01_Scripts/Monster/MonsterController.cs)<br>[MonsterPatternSO.cs](Assets/01_Scripts/Monster/Patterns/MonsterPatternSO.cs)<br>[Patterns/](Assets/01_Scripts/Monster/Patterns/) |
| 격자 전투·선입력 | 이동과 공격 중 하나를 고르게 하고, 행동 사이에 누른 입력을 한 칸짜리 슬롯에 저장했다가 바로 이어서 실행합니다. | [PlayerController.cs](Assets/01_Scripts/Player/PlayerController.cs)<br>[PlayerInput.cs](Assets/01_Scripts/Player/PlayerInput.cs)<br>[Combat/](Assets/01_Scripts/Combat/) |
| 전장의 안개 | 가 본 곳과 지금 보이는 곳만 드러나고, 벽 너머는 가려집니다. 안개 경계는 마스크 텍스처와 셰이더로 부드럽게 그립니다. | [FogOfWarSystem.cs](Assets/01_Scripts/Dungeon/FogOfWarSystem.cs)<br>[FogOfDungeonMist.shader](Assets/Resources/Shader/FogOfDungeonMist.shader) |

전체 구조도, 씬 간 데이터 흐름, 시스템별 클래스 구성은 [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)에 정리했습니다.

## 폴더 구조

```
Assets/01_Scripts/
├─ Combat/    공격 판정, 투사체, 드랍, 타격 피드백
├─ Core/      타일 점유, 길찾기, 카메라, 전역 이벤트(GlobalEvents)
├─ Data/      ScriptableObject 데이터(몬스터, 무기, 아이템)와 저장 데이터
├─ Dungeon/   던전 구성, 전장의 안개, 탈출 포탈
├─ Editor/    CSV 데이터 임포터 (현재 비활성)
├─ Hub/       기지 상호작용, 창고
├─ Monster/   몬스터 행동 루프, 이동 성향, 공격 패턴(Patterns/)
├─ Object/    안개 가시성, 바닥 아이템
├─ Player/    입력, 이동, 선입력, 스탯, 인벤토리·장비
└─ UI/        인벤토리·장비·창고 UI

Docs/
├─ ARCHITECTURE.md  전체 구조 문서
└─ Architecture/    시스템별 상세 명세(ScriptMaps), 기술 부채 목록
```

## 개발 방식

- 1인 개발로 진행하며, AI 코딩 에이전트를 함께 사용했습니다.
- 에이전트용 개발 지침(`.agy/rules/`)을 직접 작성해, 작업 전 지시 확인, 문서와 코드 동기화, 커밋 규칙 등을 지키도록 운영했습니다.
- 브랜치는 `develop`에서 작업 브랜치를 만들어 작업하고, `develop`으로 PR을 올리는 방식으로 관리합니다.
