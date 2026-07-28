# Void Pouch 아키텍처 로드맵 (Architecture Guidelines)

본 문서는 'Void Pouch' 프로젝트의 코드가 꼬이는 것을 방지하기 위한 **기본 설계 원칙(Core Rules)**과, 개발 속도를 위한 **MVP 설계(Phase 1)** 및 **최종 지향점(Phase 2)**을 명시한 가이드라인입니다.

---

## 🏗️ 0. 기본 코드 설계 원칙 및 컨벤션 (Core Architecture Plan)
이 원칙은 Phase 1(MVP)과 Phase 2(Final)에 상관없이 게임 전체를 관통하는 핵심 뼈대입니다.

### 1. 디자인 패턴: 역할 분리 (Simplified MVC)
게임은 UI(가방) 조작과 데이터 연산이 매우 중요하므로, 데이터를 다루는 부분과 화면을 그리는 부분을 철저히 분리합니다.
* **Model (순수 데이터)**: `MonoBehaviour`를 상속받지 않는 순수 C# 클래스. 유니티 에디터와 무관하게 돌아가는 논리 공간.
* **Manager (통제탑)**: 게임의 규칙을 관장하며, Model의 데이터를 수정하고 이벤트를 발생시킵니다.
* **View (UI 및 시각화)**: `MonoBehaviour`를 상속받아 화면을 그리는 스크립트. 스스로 판단(연산)하지 않고, Manager의 이벤트만 듣고 화면을 갱신(Observer 패턴)합니다.

### 2. 객체 지향 원칙: 상속과 인터페이스 (OOP Design)
코드의 확장성(Extensibility)과 가독성을 극대화하기 위해 다형성을 적극 활용합니다.
* **인터페이스 (Interfaces)**: 명칭은 반드시 `I`로 시작합니다.
  * `IDamageable`: 데미지를 받을 수 있는 모든 객체 구현 (`void TakeDamage(int amount);`)
  * `IInteractable`: 맵 상호작용 객체 구현 (`void Interact(GameObject interactor);`)
* **상속 (Inheritance)**:
  * **아이템**: 최상위 `BaseItem`을 두고, `WeaponItem`, `ConsumableItem` 등으로 파생.
  * **몬스터**: `BaseMonster`에 체력/사망 로직을 짜고, `StationaryMonster`, `BossMonster` 등으로 상속시켜 특정 패턴 재정의.

### 3. 폴더 구조 규격 (Folder Structure)
`Assets/Scripts/` 하위 폴더를 다음과 같이 엄격하게 나누어 코드를 배치합니다.
* 📁 `Core/` : 심장부. (`GameManager`, `DataManager`, `PoolManager`, 인터페이스 모음)
* 📁 `Data/` : 순수 데이터 클래스 및 CSV 매핑 모델. (`ItemData`, `MonsterData`)
* 📁 `Managers/` : 시스템 총괄 관리자. (`InventoryManager`, `CombatManager`)
* 📁 `UI/` : 화면 렌더링 담당. (`InventoryUI`, `CombatPanelUI`)
* 📁 `Gameplay/` : 맵 탐험, 전투 로직(`FSM`), 인터랙션 구현부. (`BaseMonster`)

### 4. 네이밍 컨벤션 (Naming Conventions)
* **클래스 및 메서드 (Class & Method)**: `PascalCase` (예: `InventoryManager`)
* **인터페이스 (Interface)**: `I` + `PascalCase` (예: `IInteractable`)
* **비공개 멤버 변수 (Private Field)**: `_` + `camelCase` (예: `_currentMana`)
* **지역 변수 (Local Variable)**: `camelCase` (예: `startX`)

### 5. 주석 작성 규칙 (Commenting Rules)
* **XML 주석 (`///`) 필수**: 외부에 노출되는 `public` 멤버에 달아 목적과 파라미터를 명시합니다.
* **'Why(왜)' 위주의 주석**: 코드가 '무엇'을 하는지보다, **'왜' 그렇게 작성했는지(기획적 의도, 메모리 최적화 목적 등)**를 남깁니다.
* **TODO 태그**: 당장 구현하지 않지만 추후 확장해야 할 부분은 `// TODO: [내용]` 형태로 남깁니다.

---

## 🚀 Phase 1: MVP 프로토타입 설계 (초기 개발용)
**목표:** 가장 빠르게 핵심 루프를 구현하여 게임의 재미 유무를 테스트합니다.

### MVP 특화 핵심 룰 (Must-Have)
* **FSM (상태 기계)**: 거대한 `switch-case`문을 절대 금지합니다.
  * `IState`(Enter, Update, Exit)를 구현하는 독립 클래스로 분리하여 단일 책임 원칙(SRP) 준수.
  * `StateMachine`과 공유 데이터인 `Context`를 명확히 분리.
  * 상태 객체는 `new`로 매번 생성하지 않고 딕셔너리에 캐싱하여 GC 스파이크 방지.
* **옵저버 패턴 (Observer)**: 결합도를 낮추기 위해 `SendMessage`나 `UnityEvent` 대신 가벼운 C# `event Action` 사용.
  * **메모리 누수 방지**: View 스크립트는 `OnEnable`에서 구독하고 `OnDisable`/`OnDestroy`에서 무조건 구독 해제(`-=`) 강제.
  * **안전망**: 이벤트 호출 시 null 조건부 연산자(`?.Invoke()`)를 사용하여 구독자가 없을 때의 에러 방지.
* **오브젝트 풀링 (Object Pool)**: UI 타일, 데미지 텍스트 재사용 필수.
* **데이터 POCO 분리**: CSV 데이터는 시작 시 딕셔너리로 캐싱.
* **빠른 검증 타협점**: `Resources.Load` 사용, 단일 `GameManager` 중심, View는 단순 이벤트 구독.
* **복합 패턴 적용**: FSM 내부 상태(예: 죽음)에서 UI를 직접 참조하지 않고 `Action` 이벤트를 쏘아 외부가 반응하게 결합도 완화.

---

## 🌟 Phase 2: 최종 지향점 (Final Architecture)
**목표:** 게임성이 검증된 후 진행할 정식 라이브 서비스용 대규모 리팩토링 목표입니다.

* **Addressables + UniTask**: 에셋 로딩을 Addressables로 전환하고 씬 전환 프리징 방지를 위해 `async/await` 도입.
* **DI / Service Locator**: 매니저 싱글톤을 폐기하고 의존성 주입(DI) 컨테이너 도입.
* **MVP UI 엄격 분리**: Model-View-Presenter로 쪼개어 로직과 UI 완벽 격리.
* **직렬화(세이브) 시스템 분리**: 런타임 변동 데이터는 JSON 형태의 직렬화용 클래스로 완전히 독립.
