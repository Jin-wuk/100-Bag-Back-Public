# 🏛️ 시스템 아키텍처 명세서 (System Architecture)

본 문서는 Dimension Looter 프로젝트의 핵심 뼈대인 매니저 구조, 데이터 파이프라인, 그리고 UI-로직 분리(Event Flow) 원칙을 규정합니다. 새로운 기능을 추가하거나 버그를 추적할 때, 이 문서가 시스템 간의 관계를 파악하는 나침반 역할을 합니다.

---

## 1. 매니저(Manager) 구조와 생명주기 (Hierarchy & Lifecycle)

클라이언트의 뼈대를 이루는 핵심 시스템들은 각자의 생명주기와 책임(SRP)을 가집니다.

*   **`GameManager` (전역 싱글톤 / Global)**
    *   **역할**: 게임의 거시적 상태(로비, 던전 탐험, 보스전, 결과창 등)를 관장하는 최고 관리자.
    *   **생명주기**: 게임 시작 시 생성되어 `DontDestroyOnLoad` 처리되며, 게임이 꺼질 때까지 파괴되지 않습니다.
*   **`InventoryManager` (전역 싱글톤 / Global)**
    *   **역할**: 가방(MainGrid), 보안 파우치(SecureGrid) 등 유저의 런타임 인벤토리 데이터를 메모리에 쥐고 있는 시스템.
    *   **생명주기**: 전역 싱글톤. 씬이 전환되어도 유저가 획득한 아이템 데이터는 유지되어야 하므로 파괴되지 않습니다.
*   **`CombatManager` (씬 종속 / Scene-Specific)**
    *   **역할**: 던전 씬(전투) 내에서의 몬스터 체력 관리, 스폰, 턴/실시간 FSM 제어, 그리고 사망 시 보상(파우치) 계산을 담당합니다.
    *   **생명주기**: 던전 씬에 들어올 때마다 새로 생성(Awake)되고, 기지(Lobby)로 돌아가거나 씬이 파괴될 때 함께 소멸하여 메모리를 반환합니다.

---

## 2. 데이터 아키텍처 (정적 데이터 vs 동적 데이터)

서버가 없는 스탠드얼론 클라이언트 게임이므로, **변하지 않는 기획 데이터**와 **유저의 플레이 데이터**를 엄격히 분리하여 관리합니다.

### A. 정적 데이터 (Static / Master Data)
*   **성격**: 아이템의 스펙(공격력, 가방 크기), 몬스터 드랍 테이블 등 기획자에 의해 밸런싱되는 불변 데이터.
*   **파이프라인**: 
    1. 기획자가 `CSV` 또는 `JSON` 형태로 작성.
    2. 게임 로딩(초기화) 시 파싱되어 C#의 순수 객체(`POCO`, 예: `ItemData`)로 변환.
    3. `DataManager` 내의 딕셔너리(`Dictionary<int, ItemData>`)에 메모리 캐싱되어 전역에서 읽기 전용(Read-Only)으로 참조됨.

### B. 동적 데이터 (Save / User Data)
*   **성격**: 인벤토리 내 아이템들의 그리드 좌표(GridX, GridY), 소지 골드, 장착 중인 가방, 보안 파우치 확장 단계 등.
*   **파이프라인**:
    1. 런타임 도중에는 `InventoryManager` 등의 메모리 변수로 존재.
    2. 특정 분기(던전 클리어, 로비 진입, 혹은 오토 세이브) 시점에 **하나의 세이브 객체(SaveData 클래스)**로 직렬화(Serialize).
    3. `System.Text.Json` 등을 활용해 `.json` 또는 `.dat` 형식으로 로컬 스토리지에 저장.
    4. **보안(Anti-Tamper)**: 세이브 파일 조작을 막기 위해 파일 내용 기반의 체크섬(Hash)을 추가하거나 AES 등 가벼운 암호화를 적용하여 무결성을 검증합니다.

---

## 3. UI와 로직의 분리 (Event Flow)

스파게티 코드를 방지하는 최우선 원칙: **"UI는 스스로 상태를 결정(연산)하지 않고, 오직 데이터 매니저의 이벤트(Event)를 구독(Subscribe)하여 화면을 갱신하기만 한다."**

### 💡 이벤트 기반(Observer) 통신 흐름 예시

**[상황]: 유저가 바닥에 떨어진 파우치에서 아이템을 주워 인벤토리에 넣었을 때**

1.  **입력 및 검증 (View -> Manager)**
    *   `DraggableItemUI` (UI 스크립트)가 마우스 드롭 입력을 감지합니다.
    *   UI는 판단하지 않고 `InventoryManager`에게 "이 아이템을 `[2, 3]` 위치에 넣을 수 있는지" 묻습니다 (`CanPlaceItem`).
2.  **데이터 변경 (Manager 내부)**
    *   `InventoryManager`는 데이터(Grid)를 확인하고, 배치가 가능하면 배열 데이터를 갱신(`PlaceItem`)합니다.
3.  **이벤트 발송 (Manager -> Observer)**
    *   데이터 갱신이 무사히 완료되면, `InventoryManager`는 C# `event Action`인 `OnInventoryChanged` (또는 `OnGridResized`)를 `Invoke()` 합니다.
4.  **UI 렌더링 (View 갱신)**
    *   이 이벤트를 미리 구독(`+=`)해두었던 `InventoryGridUI` 스크립트들이 이벤트를 수신합니다.
    *   이때 비로소 UI는 `InventoryManager`의 최신 배열 데이터를 읽어와서 아이템의 아이콘 좌표(`anchoredPosition`)를 새로고침(Re-render)합니다.

이러한 단방향 흐름(Action -> State Update -> Event -> View Update)을 유지하면, 추후 UI 디자인이 완전히 바뀌거나 새로운 패널이 추가되더라도 `Manager`의 핵심 로직은 단 한 줄도 수정할 필요가 없습니다.
