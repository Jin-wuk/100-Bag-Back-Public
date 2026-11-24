# 100 Bag Back

기지에서 가방을 챙겨 층계 던전을 한 층씩 올라가며 전리품을 모으고, 탈출 포탈까지 살아서 돌아오면 전리품을 지키는 **익스트랙션 실시간 타일 액션 RPG**입니다.
던전에서 죽으면 가져간 아이템을 잃기 때문에, 칸 수가 제한된 가방을 떠안고 더 올라갈지 지금 나갈지 판단하는 것이 핵심입니다.

▶ [전체 플레이 영상 보기 (Google Drive)](https://drive.google.com/file/d/18TEKzDzADUNUeY7j2v0o-A_lCcQ03sIT/view?usp=sharing)

- **개발 형태**: 1인 개인 프로젝트 (기획·개발), 2026.07 ~
- **엔진**: Unity 2022.3.62f3 (2D), C#
- **현재 단계**: 1차 마일스톤(MVP) 완료. 지역 1(숲) 10층 던전과 중간 보스·보스, 전리품 판매, 제작소 테크트리로 이어지는 정비 → 전투 → 판매·해금 사이클

> 이 레포는 개발 레포의 커밋 이력을 그대로 옮긴 공개본입니다. 재배포가 금지된 외부 에셋만 전체 이력에서 제외했습니다.

## 실행 방법

1. Unity Hub에서 **Unity 2022.3.62f3**으로 이 프로젝트 폴더를 엽니다.
2. `Assets/Scenes/02_HubScene.unity`를 열고 Play를 누릅니다.
3. 기지의 던전 포탈을 누르면 월드맵이 열립니다. 지역 아이콘 → 입장 패널 → `Enter`로 `03_DungeonScene`에 들어갑니다. 위층 계단으로 층을 올라가고, 탈출 포탈에 도착하거나 사망하면 기지로 돌아와 결과 화면을 봅니다.

## 아트와 에셋

캐릭터·몬스터·보스·전리품 아이콘·UI 키트는 PixelLab으로 만든 뒤 직접 다듬었고, 공격 이펙트는 코드로 그리는 픽셀 이펙트 도구로 만들었습니다.

바닥 타일 일부는 [Kenney](https://www.kenney.nl)(CC0)와 [SnowHex](https://snowhex.itch.io/dungeon-gathering)의 무료 에셋을 썼습니다. 재배포가 금지된 [GUI Parts](https://assetstore.unity.com/packages/2d/gui/icons/gui-parts-159068)(프레임 스프라이트 2개)는 레포에서 뺐습니다. 없으면 가방·퀵슬롯·창고 패널의 테두리만 비어 보이고, 넣으려면 `Assets/Art/Sprites/GUI_Parts/`에 두면 됩니다.

## 조작법

| 입력 | 동작 |
| --- | --- |
| `W` `A` `S` `D` / 방향키 | 타일 1칸씩 4방향 이동 |
| `Space` | 대시 (이동 방향으로 2칸, 쿨타임 3초) |
| 마우스 이동 | 조준 (8방향) |
| 마우스 왼쪽 클릭 | 근접 공격 (2타 콤보) |
| `1` `2` `3` | 퀵슬롯 소모품(포션) 사용 |
| `Tab` | 가방 열기 / 닫기 (열린 동안 일시정지) |
| `Esc` | 가방·패널 닫기 |
| 가방 아이템 우클릭 | 버리기 · 판매(기지) 메뉴 |
| `E` | 가방 ↔ 창고 빠른 이동 |

이동 중에는 공격할 수 없고, 공격 후 잠금은 대시로만 끊을 수 있습니다. 매 순간 "움직일지, 때릴지"를 고르게 하는 것이 전투 설계의 핵심이며, 행동 중에 누른 입력은 행동이 끝나는 순간 이어서 실행됩니다(선입력).

## 핵심 시스템

| 시스템 | 한 줄 설명 | 주요 코드 |
| --- | --- | --- |
| 몬스터 AI 패턴 | 공격 패턴을 ScriptableObject로 분리해 조합하고, 가중치 랜덤으로 고릅니다. 모든 공격은 바닥 장판으로 먼저 예고하고, 몰아 걷기·대치로 이동에 리듬을 줍니다. | [MonsterController.cs](Assets/01_Scripts/Monster/MonsterController.cs)<br>[MonsterPatternSO.cs](Assets/01_Scripts/Monster/Patterns/MonsterPatternSO.cs)<br>[Patterns/](Assets/01_Scripts/Monster/Patterns/) |
| 격자 전투·선입력·대시 | 이동과 공격 중 하나를 고르게 하고, 행동 사이에 누른 입력을 한 칸짜리 슬롯에 저장했다가 이어서 실행합니다. 2칸 대시로 포위와 공격 후 잠금을 끊습니다. | [PlayerController.cs](Assets/01_Scripts/Player/PlayerController.cs)<br>[PlayerInput.cs](Assets/01_Scripts/Player/PlayerInput.cs)<br>[Combat/](Assets/01_Scripts/Combat/) |
| 타일 점유·여러 칸 보스 | 한 칸에 한 명만 서는 점유 격자. 3×3 보스는 이동하는 동안 옛 칸과 새 칸을 모두 점유해 그림과 판정을 맞춥니다. | [TileSystem.cs](Assets/01_Scripts/Core/TileSystem.cs)<br>[Pathfinder.cs](Assets/01_Scripts/Core/Pathfinder.cs) |
| 제작소 테크트리 | 전리품 + 골드로 장비를 해금합니다. 창고·가방·파우치 재료를 합산하고, 실패하면 아무것도 차감하지 않습니다. 데이터 오류는 에디터가 경고합니다. | [CraftingSystem.cs](Assets/01_Scripts/Hub/CraftingSystem.cs)<br>[TechTreeSO.cs](Assets/01_Scripts/Data/Models/TechTreeSO.cs) |
| 층계 던전·검증 | 수작업 층 프리팹에서 실제 갈 수 있는 칸을 구해 몬스터와 상자를 배치하고, 계단까지 길이 막히지 않는지 에디터에서 검사합니다. | [FloorDesignValidator.cs](Assets/01_Scripts/Dungeon/FloorDesignValidator.cs) |
| 전장의 안개 | 가 본 곳과 지금 보이는 곳만 드러나고 벽 너머는 가려집니다. 경계는 마스크 텍스처와 셰이더로 부드럽게 그립니다. | [FogOfWarSystem.cs](Assets/01_Scripts/Dungeon/FogOfWarSystem.cs)<br>[FogOfDungeonMist.shader](Assets/Resources/Shader/FogOfDungeonMist.shader) |

전체 구조도, 씬 간 데이터 흐름, 시스템별 클래스 구성은 [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)에 정리했습니다.

## AI와 함께 개발한 방식

설계·구현은 Claude Code, 검토는 Gemini, 기획 결정과 최종 승인은 직접 맡았습니다. 기능 하나는 아래 6단계를 거칩니다.

1. **요청·확인**: Claude가 이해한 내용을 되묻고, 승인한 뒤에만 설계를 시작
2. **설계안**: 코드 그래프(graphify)로 바꿀 클래스의 영향 범위를 조사해 설계 문서 작성
3. **설계 검토**: Gemini가 검토 (기획 검토와 코드 검토에 서로 다른 모델)
4. **구현·적용**: 코드 작성, MCP for Unity로 씬·프리팹 적용과 콘솔 에러 확인
5. **구현 검증**: Gemini가 변경분을 읽고 승인 또는 수정 요청
6. **플레이·승인**: 직접 플레이해 보고 승인한 뒤 논리 단위로 나눠 커밋

| 자료 | 위치 |
| --- | --- |
| AI 작업 규칙 원본 | [CLAUDE.md](CLAUDE.md), [GEMINI.md](GEMINI.md), [Docs/Agent/](Docs/Agent/00_Agent_Integrated_Guidelines.md) |
| 검토 전담 서브에이전트 | [.claude/agents/gemini-reviewer.md](.claude/agents/gemini-reviewer.md) |
| 밸런스 점검 스킬과 보고서 | [.claude/balance-check.md](.claude/balance-check.md), [Docs/Balance/](Docs/Balance/) |
| 기능별 설계안 | [Docs/Architecture/](Docs/Architecture/) (층계 던전, 보스, 다수 전투와 대시, 제작소 테크트리, 보스 크기와 이동 템포, UI 키트) |
| 기획 문서 | [Docs/GDD/](Docs/GDD/GDD.md) |

커밋 메시지는 한글 Conventional Commits를 따르고, Gemini 검토를 반영한 수정은 `fix: Gemini 검토 반영 - ...` 커밋으로 따로 남겼습니다.

## 폴더 구조

```
Assets/01_Scripts/
├─ Combat/    공격 판정, 투사체, 드랍, 타격 피드백
├─ Core/      타일 점유, 길찾기, 카메라, 전역 이벤트(GlobalEvents), 일시정지, 피해 공식
├─ Data/      ScriptableObject 데이터(몬스터, 무기, 아이템, 테크트리)와 저장 데이터
├─ Dungeon/   층계 던전 생성·층 전환·상자, 층 검증, 전장의 안개, 탈출 포탈
├─ Editor/    층 회색 박스 빌더, 테크트리·프리팹 생성 메뉴
├─ Hub/       기지 상호작용, 창고, 판매·구매, 제작소
├─ Monster/   몬스터 행동 루프, 이동 성향, 공격 패턴(Patterns/), 여러 칸 점유
├─ Object/    안개 가시성, 바닥 아이템
├─ Player/    입력, 이동, 대시, 선입력, 스탯, 가방·장비·파우치·퀵슬롯·포션
└─ UI/        가방·장비·창고·파우치 UI, 핫바, HP/MP 오브, 층 HUD, 월드맵, 테크트리, 상점·결과 화면

Docs/
├─ ARCHITECTURE.md  전체 구조 문서
├─ Architecture/    기능별 설계안, 시스템별 상세 명세(ScriptMaps), 기술 부채 목록
├─ GDD/             기획 문서 (현재 구현 기준) + 로드맵·아이디어 백로그
├─ Balance/         밸런스 점검 보고서
└─ Agent/           AI 에이전트용 개발 지침
```
