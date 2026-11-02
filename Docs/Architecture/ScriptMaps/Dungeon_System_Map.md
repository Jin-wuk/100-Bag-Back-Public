# 던전 시스템 (Dungeon System) 구조도

던전 생성, 플로우, 탈출에 이르는 전반적인 시스템의 클래스/데이터 구조 맵입니다.

## 1. 아키텍처 다이어그램 (Architecture Diagram)

```mermaid
graph TD
    %% Scriptable Objects
    subgraph DataLayer["Data Layer"]
        DD["DungeonData.cs<br>- Chunk Layout<br>- Monster/Trap/Exit Prefabs"]
        RC["RegionConfigSO.cs<br>- 1~5 / 6~9 Design Pools<br>- 5F/10F Boss Designs<br>- 1~9F Exit/Chest Rules"]
        FD["FloorDesignSO.cs<br>- Floor Prefab / Band / Weight"]
    end

    %% Managers & Core Logic
    subgraph LogicLayer["Logic Layer"]
        DG["DungeonGenerator.cs<br>- Floor Path (RegionConfigSO) / FixedMap Fallback<br>- Flood Fill (BFS) & Zone Spawning"]
        FV["FloorDesignValidator.cs<br>- Marker Count / Walkable / Stairs-Exit Distance"]
        FOW["FogOfWarSystem.cs<br>- 3-State Fog: Unexplored / Explored / Visible<br>- Bresenham LOS & Dynamic Mask Texture<br>- North 2-Tile Elevation Offset"]
        LS["LootSystem.cs<br>- Field Loot Spawner<br>- Auto FogVisibilityObject Attach"]
    end

    %% Views & Objects
    subgraph ViewLayer["View / Object Layer"]
        FL["FloorLayout.cs<br>- Floor Prefab Root<br>- Floor/Wall Tilemaps, Markers, Zones"]
        FM["FloorMarker.cs<br>- PlayerStart / UpStairs / Exit / ChestCandidate"]
        DZ["DungeonZone.cs<br>- Floor Path: Combat / Corridor<br>- Box Bounds & Gizmos"]
        DC["DungeonChunk.cs<br>- Post-MVP 개별 청크"]
        DE["DungeonExit.cs<br>- Trigger Detection<br>- Scene Transition"]
        UI["DungeonEntranceUI.cs<br>- Enter Dungeon"]
        FVO["FogVisibilityObject.cs<br>- VisibleOnly / RememberWhenExplored<br>- Renderer Culling Sync"]
        FVS["FogVisionSource.cs<br>- Torch / Shrine Light Source<br>- RequireDiscovered Option"]
        SHADER["FogOfDungeonMist.shader<br>- Bilinear Mask Sampling<br>- Smooth Edge Gradient"]
    end

    %% Dependencies
    UI -->|LoadScene| DG
    DG -->|Floor: Reads Config| RC
    RC -->|Pools| FD
    FD -->|Prefab| FL
    DG -->|Floor: Instantiate under FloorGrid| FL
    FL -->|Markers| FM
    FL -->|Zones| DZ
    DG -->|Validate Instance| FV
    DG -->|FixedMap: Flood Fill & Zone 스캔| DZ
    DG -->|FixedMap: Instantiate| DE
    DG -->|Initialize| FOW
    DG -->|Procedural: Reads Config| DD
    DG -->|Procedural: Instantiate & Init| DC
    DC -->|Instantiate Exit| DE
    FOW -->|Applies Mask| SHADER
    FOW -->|Manages Visibility| FVO
    FOW -->|Includes Lights| FVS
    LS -->|Attaches FVO| FVO
```

## 2. 핵심 클래스 역할 명세 (Class Roles)

### 2.1. `DungeonGenerator` (`MonoBehaviour`)
*   **역할**: 던전 씬 진입 시 맵 생성 및 엔티티 스폰을 전담하는 메인 매니저.
*   **층 경로 (층계 던전, `_regionConfig` 지정 시)**: 기획 `07_Floor_Based_Dungeon_Design.md`.
    *   `Awake`: 씬의 영속 `FloorGrid`(Grid) 아래 원점에 층 프리팹(`FloorLayout`)을 생성하고 `_floorTilemap`/`_wallTilemap`을 층 타일맵으로 바인딩, `Physics2D.SyncTransforms()` 후 `FloorDesignValidator`로 인스턴스 검증. Grid를 층 프리팹에 두지 않는 이유: 플레이어·몬스터가 `FindObjectOfType<Grid>()`로 Grid를 캐싱하므로 층 교체(T2) 때 Grid가 바뀌면 안 된다.
    *   `Start`: `PlayerStart` 마커에 플레이어 스폰 → BFS → `Exit`·`UpStairs` 마커 반경 `_markerSafeRadius`(체비쇼프) 및 `ChestCandidate` 칸을 스폰 금지로 예약 → `Exit` 마커에 탈출 포탈(T4 전까지 항상) → `Combat`/`Corridor` 구역과 구역 밖 복도에 몬스터·함정 → 안개 초기화. 스폰물은 층 루트 아래 `Spawned`에 모은다.
    *   현재(T1)는 `EarlyPool[0]`을 1층으로 쓴다. 층 추첨(T3), 층 전환(T2), 클리어 판정(T4)은 이후 태스크.
*   **지원 모드 (`_regionConfig`가 비었을 때)**:
    1.  `FixedMap` (MVP 현행 - 구역 & Flood Fill 기반):
        *   `Start` 구역에서 플레이어를 스폰한 뒤, 해당 타일로부터 **4방향 BFS(Flood Fill)**를 수행하여 벽 안쪽의 실제 도달 가능한 바닥 타일(`FloorTilemap` 있음 & `WallTilemap` 없음 & 장애물 없음)과 이동 걸음 수를 산출. (벽 뒤의 빈 공간 100% 배제)
        *   실제 미로 이동 걸음 수가 가장 먼 `Exit` 구역 후보 중 1곳에 탈출 포탈 생성.
        *   `Chest`, `Combat`, `Corridor` 구역 및 구역 미지정 일반 복도에 최소 간격(`_minMonsterSpacing`)과 출발 안전 반경(`_startSafeStepRadius`)을 준수하여 상자/몬스터/함정 비중복 스폰.
        *   맵 세팅 직후 `FogOfWarSystem`을 자동 초기화하여 플레이어 시작 위치 기준으로 시야를 개방.
    2.  `ProceduralChunks` (Post-MVP): `DungeonData`를 읽어 2x2 또는 3x3으로 청크 프리팹들을 절차적으로 조립 배치.

### 2.2. `FogOfWarSystem` (`MonoBehaviour`)
*   **역할**: 플레이어의 타일 이동 이벤트(`GlobalEvents.OnPlayerCellChanged`) 및 시야원들을 추적하여 3단계 전장의 안개(`Unexplored`, `Explored`, `Visible`)와 엔티티 가시성을 총괄 제어하는 코어 비전 매니저.
*   **특징**:
    *   **동적 텍스처 마스크 생성 & 셰이더 연동**: 타일 단위 계단식 깨짐을 없애기 위해 런타임에 `Texture2D`(`FilterMode.Bilinear`)를 생성하고 `FogOfDungeonMist.shader`를 입힌 풀스크린/오버레이 쿼드에 바인딩하여 부드러운 안개 그라데이션 제공.
    *   **Bresenham 직선 시야(LOS)**: 광원에서 방사되는 광선을 타일 단위로 추적하여, 벽면 타일까지는 밝혀주되 벽 너머 복도나 숨겨진 방은 시야선 차단.
    *   **2.5D 탑다운 북쪽 2타일 수직 보정**: 탑다운 뷰에서 북쪽 벽면이 2타일 높이로 표현되는 특성을 고려하여, 북쪽 벽면 2칸까지 자연스럽게 시야에 포함하는 수직 오프셋 보정(`_wallVerticalElevation`).
    *   **멀티 광원(Multi Vision Sources)**: 플레이어 기본 시야 외에 `FogVisionSource` 컴포넌트가 붙은 오브젝트(횃불, 제단)를 등록받아 통합 시야 맵을 합성.
    *   **오브젝트 가시성 중앙 관리**: `FogVisibilityObject` 컴포넌트를 등록하여 현재 시야 밖의 몬스터 및 드롭 아이템을 즉각 은폐.

### 2.3. `FogVisibilityObject` (`MonoBehaviour`)
*   **역할**: 안개 상태에 따라 자신의 렌더러(Renderer) 활성화/비활성화를 제어하는 컴포넌트.
*   **동작 모드**:
    *   `VisibleOnly`: 몬스터 및 드롭 아이템에 적용. 현재 시야(`Visible`) 내에 있을 때만 렌더러가 켜지며, 탐험된 안개(`Explored`)나 미탐험(`Unexplored`) 영역에 진입하면 즉시 은폐.
    *   `RememberWhenExplored`: 상자, 제단, 정적 상호작용물에 적용. 한 번이라도 플레이어가 밝힌 영역(`Explored` 이상)에 위치하면 계속해서 모습이 유지됨.

### 2.4. `FogVisionSource` (`MonoBehaviour`)
*   **역할**: 맵 내에 배치되는 독립적인 보조 광원(횃불, 신비한 제단, 마법 수정 등).
*   **주요 옵션**:
    *   `visionRadius`: 광원이 비추는 타일 반경.
    *   `requireDiscoveredToEmit`: 활성화 시 플레이어가 해당 광원에 직접 도달하거나 탐험하기 전에는 빛을 내지 않다가, 탐험 완료 후 영구적으로 주변을 밝힘.

### 2.5. `FogOfDungeonMist.shader` (Shader)
*   **역할**: 안개 마스크 텍스처(`_FogTex`)를 샘플링하여 미탐험 색상(`_UnexploredColor`, 완전 흑색), 기탐험 색상(`_ExploredColor`, 반투명 흑색), 가시 영역(투명)을 블렌딩하는 전용 셰이더.

### 2.6. `DungeonZone` (`MonoBehaviour`)
*   **역할**: 통짜 미로 맵 위에 사각형 영역(`_size` 또는 `BoxCollider2D`)으로 배치되어 구역의 성격(`Start`, `Combat`, `Chest`, `Corridor`, `Exit`)과 스폰 수량을 정의.
*   **층 경로**: `Combat`·`Corridor`만 쓴다. 시작점·탈출구·상자 후보는 `FloorMarker`가 맡는다(나머지 enum 값은 T3 청크 정리 때 함께 정리).
*   **특징**:
    *   에디터 씬 뷰에서 구역 타입별 색상 반투명 기즈모(Gizmo) 렌더링.
    *   런타임 시 `BoxCollider2D`가 붙어 있으면 자동으로 비활성화하여 물리 충돌 간섭 방지.

### 2.7. `DungeonChunk` (`MonoBehaviour`)
*   **역할**: 절차적 생성(Post-MVP) 시 개별 분할 청크의 내부 스폰 지점 관리 (또는 레거시 통짜맵 폴백).

### 2.8. `DungeonData` (`ScriptableObject`)
*   **역할**: 맵 생성 규칙과 기본 프리팹들의 정적 데이터 보관.

### 2.9. `DungeonExit` (`MonoBehaviour`)
*   **역할**: 탈출구(포탈)의 상호작용 및 씬 전환 처리 (`LoadSceneMode.Single`).

### 2.10. `FloorLayout` / `FloorMarker` (`MonoBehaviour`)
*   **`FloorLayout`**: 층 프리팹 루트. `Floor`·`Wall` 타일맵 참조, 마커·구역 수집(지연 캐싱, Edit Mode에서는 매번 재수집), 루트 기준 칸 좌표(`GetLocalCell`, 셀 크기 1).
*   **`FloorMarker`**: `PlayerStart` / `UpStairs` / `Exit` / `ChestCandidate`. 칸 중앙(+0.5)에 둔다.
*   **층 프리팹 규격**: 루트(`FloorLayout`, Grid 없음) 아래 `Floor`, `Wall`(레이어 Wall, `TilemapCollider2D`), `Markers`, `Zones`. 타일맵은 루트 기준 위치·회전 0, 스케일 1.

### 2.11. `FloorDesignSO` / `RegionConfigSO` (`ScriptableObject`)
*   **`FloorDesignSO`**: 층 프리팹, 구간(`Early` 1~5 / `Late` 6~9 / `Boss`), 가중치.
*   **`RegionConfigSO`**: 지역 번호·이름, 1~5 풀, 6~9 풀, 5층·10층 보스 디자인, 1~9층 규칙(`FloorRule`: 탈출구 확률, 상자 굴림 횟수·확률), 디버그 "디자인 중복 허용".

### 2.12. `FloorDesignValidator` (static)
*   **역할**: 런타임·에디터 공용 규격 검증. 시작점 1, 탈출구 1, 계단 1(10층 보스층은 0~1), 마커 칸 보행 가능·중복 없음, 계단–탈출구 체비쇼프 거리 ≥ 3, 상자 후보 ≥ 구간 최대 굴림 횟수, 풀 구간 일치.

### 2.13. `FloorGrayBoxBuilder` (Editor)
*   메뉴 `BagBack100/Floor/Create Gray Box Floors`: 회색 박스 층 3장(일반 2 + 보스 1)과 `Region1Config`·`FloorDesignSO` 생성 후 검증.
*   메뉴 `BagBack100/Floor/Validate Region Configs`: 모든 `RegionConfigSO` 검증.
*   메뉴 `BagBack100/Floor/Setup Dungeon Scene For Floors`: `03_DungeonScene`의 `FixedMap` 인스턴스 제거, `FloorGrid` 생성, `DungeonGenerator._regionConfig` 연결 후 저장.
