# 던전 시스템 (Dungeon System) 구조도

던전 생성, 플로우, 탈출에 이르는 전반적인 시스템의 클래스/데이터 구조 맵입니다.

## 1. 아키텍처 다이어그램 (Architecture Diagram)

```mermaid
graph TD
    %% Scriptable Objects
    subgraph Data Layer
        DD[DungeonData.cs\n- Chunk Layout\n- Monster/Trap/Exit Prefabs]
    end

    %% Managers & Core Logic
    subgraph Logic Layer
        DG[DungeonGenerator.cs\n- Mode: FixedMap / ProceduralChunks\n- Flood Fill (BFS) & Zone Spawning]
    end

    %% Views & Objects
    subgraph View/Object Layer
        DZ[DungeonZone.cs\n- Start / Combat / Chest / Corridor / Exit\n- Box Bounds & Gizmos]
        DC[DungeonChunk.cs\n- Post-MVP 개별 청크]
        DE[DungeonExit.cs\n- Trigger Detection\n- Scene Transition]
        UI[DungeonEntranceUI.cs\n- Enter Dungeon]
    end

    %% Dependencies
    UI -->|LoadScene| DG
    DG -->|FixedMap: Flood Fill & Zone 스캔| DZ
    DG -->|FixedMap: Instantiate| DE
    DG -->|Procedural: Reads Config| DD
    DG -->|Procedural: Instantiate & Init| DC
    DC -->|Instantiate Exit| DE
```

## 2. 핵심 클래스 역할 명세 (Class Roles)

### 2.1. `DungeonGenerator` (`MonoBehaviour`)
*   **역할**: 던전 씬 진입 시 맵 생성 및 엔티티 스폰을 전담하는 메인 매니저.
*   **지원 모드**:
    1.  `FixedMap` (MVP 현행 - 구역 & Flood Fill 기반):
        *   `Start` 구역에서 플레이어를 스폰한 뒤, 해당 타일로부터 **4방향 BFS(Flood Fill)**를 수행하여 벽 안쪽의 실제 도달 가능한 바닥 타일(`FloorTilemap` 있음 & `WallTilemap` 없음 & 장애물 없음)과 이동 걸음 수를 산출. (벽 뒤의 빈 공간 100% 배제)
        *   실제 미로 이동 걸음 수가 가장 먼 `Exit` 구역 후보 중 1곳에 탈출 포탈 생성.
        *   `Chest`, `Combat`, `Corridor` 구역 및 구역 미지정 일반 복도에 최소 간격(`_minMonsterSpacing`)과 출발 안전 반경(`_startSafeStepRadius`)을 준수하여 상자/몬스터/함정 비중복 스폰.
    2.  `ProceduralChunks` (Post-MVP): `DungeonData`를 읽어 2x2 또는 3x3으로 청크 프리팹들을 절차적으로 조립 배치.

### 2.2. `DungeonZone` (`MonoBehaviour`)
*   **역할**: 통짜 미로 맵 위에 사각형 영역(`_size` 또는 `BoxCollider2D`)으로 배치되어 구역의 성격(`Start`, `Combat`, `Chest`, `Corridor`, `Exit`)과 스폰 수량을 정의.
*   **특징**:
    *   에디터 씬 뷰에서 구역 타입별 색상 반투명 기즈모(Gizmo) 렌더링.
    *   런타임 시 `BoxCollider2D`가 붙어 있으면 자동으로 비활성화하여 물리 충돌 간섭 방지.

### 2.3. `DungeonChunk` (`MonoBehaviour`)
*   **역할**: 절차적 생성(Post-MVP) 시 개별 분할 청크의 내부 스폰 지점 관리 (또는 레거시 통짜맵 폴백).

### 2.4. `DungeonData` (`ScriptableObject`)
*   **역할**: 맵 생성 규칙과 기본 프리팹들의 정적 데이터 보관.

### 2.5. `DungeonExit` (`MonoBehaviour`)
*   **역할**: 탈출구(포탈)의 상호작용 및 씬 전환 처리 (`LoadSceneMode.Single`).
