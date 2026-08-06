# 스크립트 종속성 맵: 플레이어 시스템 (Player System)

본 문서는 플레이어의 이동, 입력 처리, 경로 탐색 및 관련 UI를 담당하는 스크립트들의 역할과 종속성을 파악하기 위한 아키텍처 지도입니다. `01_Feature_Checklist.md`의 SRP 및 디커플링 원칙을 준수하여 설계되었습니다.

## 📁 `Assets/Scripts/Player/`
플레이어 캐릭터에 부착되는 컴포넌트들로, FSM과 입력을 처리합니다.
- **`PlayerController.cs`**: 플레이어의 상태(Idle, Moving)를 관리하는 FSM이자 진입점(Facade)입니다. `PlayerInput`의 이벤트를 구독하여 `PlayerMovement`로 명령을 하달합니다.
- **`PlayerInput.cs`**: 마우스 클릭 및 호버 상태를 감지하여 타일 위치를 계산합니다. `EventSystem`을 통해 UI 클릭 시 이동을 무시하며, `MovementPreview`에 시각적 업데이트를 요청합니다.
- **`PlayerMovement.cs`**: `Pathfinder`가 반환한 경로(A*)를 바탕으로 코루틴 기반의 실제 이동(통통 튀는 Hopping 연출 포함)을 수행합니다. 타일 중앙 도달 전까지는 경로 수정을 지연시키는 제약 조건 로직이 포함되어 있습니다.

## 📁 `Assets/Scripts/Core/`
게임 전반에서 공용으로 사용될 수 있는 핵심 유틸리티 및 로직입니다.
- **`Pathfinder.cs`**: A* 알고리즘을 구현한 정적 헬퍼 클래스입니다. `Physics2D.OverlapPoint`를 사용해 장애물 레이어(`Wall`)를 판별하며, 타일 단위의 경로 리스트를 반환합니다.
- **`CameraFollow.cs`**: 설정된 대상(Target)을 부드럽게 추적(`Vector3.SmoothDamp` 사용)하는 카메라 스크립트입니다. 던전 진입 시 `PlayerController`가 스스로를 카메라의 추적 대상으로 등록(DI)합니다.

## 📁 `Assets/Scripts/UI/`
- **`MovementPreview.cs`**: 플레이어 이동 전, 마우스 커서 위치의 타일 이동 가능 여부(흰색/빨간색)를 시각적으로 표시하는 오버레이 스프라이트 로직입니다.

---
**[종속성 흐름]**
마우스 이동/클릭 ➡️ `PlayerInput` 감지 ➡️ `Pathfinder` 장애물 확인 ➡️ `MovementPreview` 표시 
➡️ 클릭 시 `PlayerController`로 이벤트 전달 ➡️ `PlayerMovement`가 경로 재탐색 및 코루틴 이동 실행 ➡️ 이동하는 플레이어 transform을 `CameraFollow`가 추적.
